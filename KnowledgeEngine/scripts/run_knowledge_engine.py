from pathlib import Path
import sys
import argparse

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src"

sys.path.insert(0, str(SRC))

from knowledge_engine.knowledge_service import KnowledgeService
from knowledge_engine.llm_provider import AnthropicProvider
from knowledge_engine.models import RetrievalRequest


def main():
    
    parser = argparse.ArgumentParser()
    parser.add_argument("--reset", action="store_true", help="Rebuild the vector database")
    args = parser.parse_args()
    
    # service = KnowledgeService(
        # documents_path=ROOT / "documents",
        # db_path=ROOT / "db",
        # llm_provider=AnthropicProvider(),
    # )
    service = KnowledgeService(
    knowledge_paths=[
        #ROOT / "documents",
        Path(r"C:\Jobs\ASPS\GitHub\Software\docs"),
        Path(r"C:\Jobs\ASPS\GitHub\Software\.claude"),
    ],
    db_path=ROOT / "db",
    llm_provider=AnthropicProvider(),
)

    # chunks_count = service.ingest_documents(reset=True)
    # print(f"Indexed {chunks_count} chunks")
    
    if args.reset:
        print(f"RESET")
        chunks_count = service.ingest_documents(reset=True)
        print(f"Indexed {chunks_count} chunks")
    else:
        print("Using existing vector database. Use --reset to rebuild.")

    while True:
        question = input("\nAsk Knowledge Engine > ")

        if question.lower() in ["q", "quit", "exit"]:
            break

        request = RetrievalRequest(
            question=question,
            #agent_role="Architect",
            top_k=5,
        )

        # answer = service.ask(request)

        # print("\nANSWER:")
        # print(answer.answer)

        # print("\nSOURCES:")
        # for index, source in enumerate(answer.sources, start=1):
            # print(
                # f"{index}. {source.source} | "
                # f"type={source.document_type} | "
                # f"chunk={source.chunk_index}"
            # )

        results = service.search(request)

        print("\nRETRIEVAL DEBUG:")
        print(f"Results count: {len(results)}")

        for index, result in enumerate(results, start=1):
            chunk = result.chunk
            print("\n" + "-" * 80)
            print(f"Result {index}")
            print(f"Source: {chunk.source}")
            print(f"Type: {chunk.document_type}")
            print(f"Chunk: {chunk.chunk_index}")
            print(f"Score/Distance: {result.score}")
            print("Preview:")
            print(chunk.text[:500])

        answer = service.ask(request)

        print("\nANSWER:")
        print(answer.answer)

        print("\nSOURCES:")
        for index, source in enumerate(answer.sources, start=1):
            print(
                f"{index}. {source.source} | "
                f"type={source.document_type} | "
                f"chunk={source.chunk_index}"
            )
    
if __name__ == "__main__":
    main()