from pathlib import Path

from knowledge_engine.chunker import Chunker
from knowledge_engine.document_loader import DocumentLoader
from knowledge_engine.llm_provider import LlmProvider
from knowledge_engine.models import KnowledgeAnswer, RetrievalRequest, RetrievalResult
from knowledge_engine.prompt_builder import PromptBuilder
from knowledge_engine.retriever import Retriever
from knowledge_engine.vector_store import VectorStore


class KnowledgeService:
    def __init__(
        self,
        knowledge_paths: list[str | Path],
        db_path: str | Path,
        llm_provider: LlmProvider,
        collection_name: str = "asps_knowledge",
    ):
        #self.documents_path = Path(documents_path)
        self.knowledge_paths = [Path(p) for p in knowledge_paths]
        self.db_path = Path(db_path)

        self.document_loader = DocumentLoader()
        self.chunker = Chunker()
        self.vector_store = VectorStore(
            db_path=self.db_path,
            collection_name=collection_name,
        )
        self.retriever = Retriever(self.vector_store)
        self.prompt_builder = PromptBuilder()
        self.llm_provider = llm_provider

    def ingest_documents(self, reset: bool = False) -> int:

        if reset:
            print("Knowledge paths:")
            for p in self.knowledge_paths:
                print(" -", p)
            
            self.vector_store.delete_collection()

        #documents = self.document_loader.load_directory(self.documents_path)
        documents = self.document_loader.load_directories(self.knowledge_paths)
        chunks = self.chunker.chunk_documents(documents)

        self.vector_store.upsert_chunks(chunks)

        return len(chunks)

    def search(self, request: RetrievalRequest) -> list[RetrievalResult]:
        return self.retriever.search(request)

    def ask(self, request: RetrievalRequest) -> KnowledgeAnswer:
        results = self.search(request)

        prompt = self.prompt_builder.build_answer_prompt(
            question=request.question,
            results=results,
        )

        answer = self.llm_provider.generate(prompt)

        return KnowledgeAnswer(
            question=request.question,
            answer=answer,
            sources=[result.chunk for result in results],
        )