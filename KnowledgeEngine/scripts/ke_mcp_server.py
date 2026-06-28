import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src"
sys.path.insert(0, str(SRC))

from mcp.server.fastmcp import FastMCP

from knowledge_engine.config import DB_PATH, ASPS_DOCS, ASPS_CLAUDE
from knowledge_engine.knowledge_service import KnowledgeService
from knowledge_engine.llm_provider import AnthropicProvider
from knowledge_engine.models import RetrievalRequest

mcp = FastMCP("ASPS Knowledge Engine")

def create_service():
    return KnowledgeService(
        knowledge_paths=[ASPS_DOCS, ASPS_CLAUDE],
        db_path=DB_PATH,
        llm_provider=AnthropicProvider(),
    )

@mcp.tool()
def knowledge_search(question: str, top_k: int = 5) -> str:
    service = create_service()
    results = service.search(RetrievalRequest(question=question, top_k=top_k))

    output = []
    for i, result in enumerate(results, start=1):
        chunk = result.chunk
        output.append(
            f"Result {i}\n"
            f"Source: {chunk.source}\n"
            f"Type: {chunk.document_type}\n"
            f"Chunk: {chunk.chunk_index}\n"
            f"Distance: {result.score}\n\n"
            f"{chunk.text[:1200]}"
        )

    return "\n\n" + ("=" * 80 + "\n\n").join(output)

@mcp.tool()
def knowledge_ask(question: str, top_k: int = 5) -> str:
    service = create_service()
    answer = service.ask(RetrievalRequest(question=question, top_k=top_k))

    sources = "\n".join(
        f"{i}. {s.source} | type={s.document_type} | chunk={s.chunk_index}"
        for i, s in enumerate(answer.sources, start=1)
    )

    return f"{answer.answer}\n\nSOURCES:\n{sources}"

if __name__ == "__main__":
    mcp.run()
    