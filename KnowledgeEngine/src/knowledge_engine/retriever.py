from typing import Any

from knowledge_engine.models import RetrievalRequest, RetrievalResult
from knowledge_engine.vector_store import VectorStore


class Retriever:
    def __init__(self, vector_store: VectorStore):
        self.vector_store = vector_store

    def search(self, request: RetrievalRequest) -> list[RetrievalResult]:
        filters = self._build_filters(request)

        return self.vector_store.search(
            query=request.question,
            top_k=request.top_k,
            filters=filters,
        )

    def _build_filters(self, request: RetrievalRequest) -> dict[str, Any] | None:
        filters: dict[str, Any] = {}

        if request.project:
            filters["project"] = request.project

        if request.component:
            filters["component"] = request.component

        if request.document_types:
            # Chroma supports $in for filtering multiple values
            filters["document_type"] = {"$in": request.document_types}

        if request.agent_role:
            filters["agent_role"] = request.agent_role

        if not filters:
            return None

        return filters