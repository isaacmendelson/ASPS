from knowledge_engine.models import RetrievalResult


class PromptBuilder:
    def build_answer_prompt(
        self,
        question: str,
        results: list[RetrievalResult],
    ) -> str:
        context = self._build_context(results)

        return f"""
You are the ASPS Knowledge Engine.

Answer the user's question based ONLY on the provided context.
If the context does not contain enough information, say so clearly.
Do not invent facts.

Question:
{question}

Context:
{context}

Answer format:
1. Direct answer
2. Key supporting points
3. Sources used
""".strip()

    def _build_context(self, results: list[RetrievalResult]) -> str:
        context_parts: list[str] = []

        for index, result in enumerate(results, start=1):
            chunk = result.chunk

            context_parts.append(
                f"""
[Source {index}]
Source: {chunk.source}
Document Type: {chunk.document_type}
Chunk: {chunk.chunk_index}

{chunk.text}
""".strip()
            )

        return "\n\n---\n\n".join(context_parts)