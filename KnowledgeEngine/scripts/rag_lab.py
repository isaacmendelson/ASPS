import os
from pathlib import Path
import chromadb
from pypdf import PdfReader
from docx import Document
from dotenv import load_dotenv
from anthropic import Anthropic

#load_dotenv()
#client = Anthropic()

#for model in client.models.list():
    #print(model.id)
    
api_key = os.getenv("ANTHROPIC_API_KEY")

if not api_key:
    raise RuntimeError(
        "Missing ANTHROPIC_API_KEY. Set it before running the script."
    )

client = Anthropic(api_key=api_key)

ROOT = Path(__file__).resolve().parent.parent
DOCS_DIR = ROOT / "documents"
DB_DIR = ROOT / "db"

COLLECTION_NAME = "asps_knowledge"


def ask_llm(question, retrieved_docs, metadatas):
    
    print("ask_llm: ", question)
    client = Anthropic()

    context_parts = []
    for i, doc in enumerate(retrieved_docs):
        meta = metadatas[i]
        context_parts.append(
            f"[Source {i+1}: {meta.get('source')} | chunk {meta.get('chunk')}]\n{doc}"
        )

    context = "\n\n---\n\n".join(context_parts)

    prompt = f"""
You are the ASPS Knowledge Engine.

Answer the user's question based ONLY on the context below.
If the answer is not in the context, say that the available documents do not contain enough information.

Question:
{question}

Context:
{context}

Answer with:
1. Direct answer
2. Supporting points
3. Sources used
"""

    response = client.messages.create(
        model="claude-haiku-4-5-20251001",
        max_tokens=1200,
        messages=[
            {"role": "user", "content": prompt}
        ]
    )

    return response.content[0].text
    
def read_txt(path):
    return path.read_text(encoding="utf-8", errors="ignore")


def read_docx(path):
    doc = Document(path)
    return "\n".join(p.text for p in doc.paragraphs if p.text.strip())


def read_pdf(path):
    reader = PdfReader(str(path))
    pages = []
    for page in reader.pages:
        text = page.extract_text() or ""
        pages.append(text)
    return "\n".join(pages)


def read_file(path):
    ext = path.suffix.lower()
    if ext in [".txt", ".md"]:
        return read_txt(path)
    if ext == ".docx":
        return read_docx(path)
    if ext == ".pdf":
        return read_pdf(path)
    return ""


def chunk_text(text, chunk_size=800, overlap=150):
    text = " ".join(text.split())
    chunks = []
    start = 0

    while start < len(text):
        end = start + chunk_size
        chunk = text[start:end]
        if len(chunk.strip()) > 100:
            chunks.append(chunk)
        start += chunk_size - overlap

    return chunks


def index_documents():
    client = chromadb.PersistentClient(path=str(DB_DIR))
    collection = client.get_or_create_collection(name=COLLECTION_NAME)

    ids = []
    docs = []
    metadatas = []

    for path in DOCS_DIR.rglob("*"):
        if not path.is_file():
            continue

        if path.suffix.lower() not in [".txt", ".md", ".docx", ".pdf"]:
            continue

        text = read_file(path)
        if not text.strip():
            continue

        chunks = chunk_text(text)

        for i, chunk in enumerate(chunks):
            ids.append(f"{path.name}-{i}")
            docs.append(chunk)
            metadatas.append({
                "source": path.name,
                "chunk": i,
                "path": str(path)
            })

    if docs:
        collection.upsert(
            ids=ids,
            documents=docs,
            metadatas=metadatas
        )

    print(f"Indexed {len(docs)} chunks from {DOCS_DIR}")


def query(question, n_results=5):
    client = chromadb.PersistentClient(path=str(DB_DIR))
    collection = client.get_or_create_collection(name=COLLECTION_NAME)

    results = collection.query(
        query_texts=[question],
        n_results=n_results
    )

    print("\nQUESTION:")
    print(question)
    #print(os.getenv("ANTHROPIC_API_KEY"))
    # print("\nTOP RESULTS:")
    # for i, doc in enumerate(results["documents"][0]):
        # metadata = results["metadatas"][0][i]
        # print("\n" + "=" * 80)
        # print(f"Result {i + 1}")
        # print(f"Source: {metadata['source']}, chunk: {metadata['chunk']}")
        # print("-" * 80)
        # print(doc[:1200])
        
    docs = results["documents"][0]
    metadatas = results["metadatas"][0]

    answer = ask_llm(question, docs, metadatas)

    print("\nANSWER:")
    print(answer)

    print("\nSOURCES:")
    for i, metadata in enumerate(metadatas):
        print(f"{i+1}. {metadata['source']} | chunk {metadata['chunk']}")

if __name__ == "__main__":
    index_documents()

    while True:
        q = input("\nAsk ASPS Knowledge Engine > ")
        if q.lower() in ["exit", "quit", "q"]:
            break
        query(q)