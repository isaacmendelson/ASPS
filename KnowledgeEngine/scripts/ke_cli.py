from pathlib import Path
import sys
import argparse

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src"

sys.path.insert(0, str(SRC))

from knowledge_engine.knowledge_service import KnowledgeService
from knowledge_engine.llm_provider import AnthropicProvider
from knowledge_engine.models import RetrievalRequest
from knowledge_engine.config import (
    ASPS_DOCS,
    ASPS_CLAUDE,
    DB_PATH,
)

KNOWLEDGE_SOURCES = [
    ASPS_DOCS,
    ASPS_CLAUDE,
]

# def create_service() -> KnowledgeService:
    # return KnowledgeService(
        # knowledge_paths=[
            # #ROOT / "documents",
            # #Path(r"C:\AI\Projects\ASPS\.claude"),
            # Path(r"C:\Jobs\ASPS\GitHub\Software\docs"),
            # Path(r"C:\Jobs\ASPS\GitHub\Software\.claude"),
        # ],
        # db_path=ROOT / "db",
        # llm_provider=AnthropicProvider(),
    # )

# def create_service() -> KnowledgeService:
    # return KnowledgeService(
        # knowledge_paths=[
            # ASPS_DOCS,
            # ASPS_CLAUDE,
        # ],
        # db_path=DB_PATH,
        # llm_provider=AnthropicProvider(),
    # )
def create_service() -> KnowledgeService:
    service = KnowledgeService(
        knowledge_paths=KNOWLEDGE_SOURCES,
        db_path=DB_PATH,
        llm_provider=AnthropicProvider(),
    )
    return service
    
def cmd_index(args):
    service = create_service()
    count = service.ingest_documents(reset=args.reset)
    print(f"Indexed {count} chunks")


def cmd_search(args):
    service = create_service()

    request = RetrievalRequest(
        question=args.question,
        top_k=args.top_k,
    )

    results = service.search(request)

    for i, result in enumerate(results, start=1):
        chunk = result.chunk
        print("=" * 80)
        print(f"Result {i}")
        print(f"Source: {chunk.source}")
        print(f"Type: {chunk.document_type}")
        print(f"Chunk: {chunk.chunk_index}")
        print(f"Score/Distance: {result.score}")
        print("-" * 80)
        print(chunk.text[:args.preview])


def cmd_ask(args):
    service = create_service()

    request = RetrievalRequest(
        question=args.question,
        top_k=args.top_k,
    )

    answer = service.ask(request)

    print(answer.answer)

    if args.sources:
        print("\nSOURCES:")
        for i, source in enumerate(answer.sources, start=1):
            print(
                f"{i}. {source.source} | "
                f"type={source.document_type} | "
                f"chunk={source.chunk_index}"
            )


def main():
    parser = argparse.ArgumentParser(
        description="Knowledge Engine CLI"
    )

    subparsers = parser.add_subparsers(dest="command", required=True)

    index_parser = subparsers.add_parser("index")
    index_parser.add_argument("--reset", action="store_true")
    index_parser.set_defaults(func=cmd_index)

    search_parser = subparsers.add_parser("search")
    search_parser.add_argument("question")
    search_parser.add_argument("--top-k", type=int, default=5)
    search_parser.add_argument("--preview", type=int, default=800)
    search_parser.set_defaults(func=cmd_search)

    ask_parser = subparsers.add_parser("ask")
    ask_parser.add_argument("question")
    ask_parser.add_argument("--top-k", type=int, default=5)
    ask_parser.add_argument("--sources", action="store_true")
    ask_parser.set_defaults(func=cmd_ask)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()