import sys
import threading
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src"
sys.path.insert(0, str(SRC))

from mcp.server.fastmcp import FastMCP

from knowledge_engine.config import DB_PATH, ASPS_DOCS, ASPS_CLAUDE
from knowledge_engine.knowledge_service import KnowledgeService
from knowledge_engine.llm_provider import AnthropicProvider, OllamaProvider
from knowledge_engine.models import RetrievalRequest

mcp = FastMCP("ASPS Knowledge Engine")

def create_service() -> KnowledgeService:
    """Build a brand-new KnowledgeService.

    Expensive: opens the chromadb PersistentClient at DB_PATH, resolves/creates
    the collection, and constructs the retriever + LLM provider. MCP tool
    handlers must NOT call this directly -- they go through get_service() so
    the cost is paid once per server process, not once per query. (ke_cli.py
    has its own separate create_service() for the index/ingest path and is
    intentionally not coupled to this module's singleton.)
    """
    return KnowledgeService(
        knowledge_paths=[ASPS_DOCS, ASPS_CLAUDE],
        db_path=DB_PATH,
        llm_provider=OllamaProvider(),
    )


# --- Shared singleton --------------------------------------------------
# ASPS-781: knowledge_search/knowledge_ask used to call create_service() on
# every invocation, reopening the chromadb collection and reloading the
# embedder each time -- causing a cold-start CONNECT_TIMEOUT on the first
# call and avoidable per-query latency thereafter. Build it once and reuse.
#
# Thread-safety: once constructed, KnowledgeService (and the VectorStore /
# Retriever / OllamaProvider it wraps) holds no per-call mutable state --
# `search`/`ask` only read from chromadb and make a stateless HTTP call to
# Ollama. Concurrent reads against one VectorStore instance were verified
# empirically (12 concurrent threads querying the real on-disk collection,
# no errors -- see ASPS-781 handoff). The lock below therefore only guards
# the lazy *construction* of the singleton (double-checked locking), so a
# burst of concurrent first calls can't race into building two instances;
# it does not serialize queries once the instance exists.
_service: KnowledgeService | None = None
_service_lock = threading.Lock()


def get_service() -> KnowledgeService:
    """Return the shared KnowledgeService, constructing it on first use."""
    global _service
    if _service is None:
        with _service_lock:
            if _service is None:
                _service = create_service()
    return _service


def warm_service() -> KnowledgeService:
    """Force full initialization of the retrieval stack at startup.

    Constructing the singleton opens the chromadb collection; running one
    real (cheap) search additionally forces chromadb's embedding function to
    load, so the first real MCP tool call doesn't pay that cold-start cost.
    Any failure here is intentionally NOT caught -- a broken retrieval stack
    must fail loudly at boot, not silently on the first user query.
    """
    service = get_service()
    service.search(RetrievalRequest(question="warmup", top_k=1))
    return service


@mcp.tool()
def knowledge_search(question: str, top_k: int = 5) -> str:
    service = get_service()
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
    service = get_service()
    answer = service.ask(RetrievalRequest(question=question, top_k=top_k))

    sources = "\n".join(
        f"{i}. {s.source} | type={s.document_type} | chunk={s.chunk_index}"
        for i, s in enumerate(answer.sources, start=1)
    )

    return f"{answer.answer}\n\nSOURCES:\n{sources}"

if __name__ == "__main__":
    warm_service()
    mcp.run()
    