"""ASPS-781 -- regression tests for the MCP server's service lifecycle.

KnowledgeEngine had no test harness before this change (tests/ only held an
empty __init__.py, no pytest config, pytest not installed in the venv). These
tests use stdlib `unittest` only, so they run directly with the KE venv
python (`KnowledgeEngine/.venv/Scripts/python.exe -m unittest discover -s
KnowledgeEngine/tests`) with no new dependency.

Covers:
  - knowledge_search / knowledge_ask reuse ONE KnowledgeService instance
    across multiple calls instead of rebuilding it per call (the ASPS-781 bug).
  - warm_service() forces initialization eagerly (so the first real tool
    call doesn't pay the cold-start cost) and does NOT swallow a failure --
    a broken retrieval stack must raise at boot.

Deferred (needs a live chromadb + Ollama, not exercised here):
  - Actual chromadb collection open latency / embedder load timing.
  - Concurrency was instead verified empirically against the real on-disk
    chroma collection outside this test module (see ASPS-781 handoff) --
    KnowledgeService/VectorStore/Retriever hold no per-call mutable state,
    so no query-time lock was added; only the lazy singleton construction
    is guarded by a lock (double-checked locking) to avoid a construction
    race on the very first concurrent calls.
"""

import sys
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / "src"
SCRIPTS = ROOT / "scripts"

for _p in (str(SRC), str(SCRIPTS)):
    if _p not in sys.path:
        sys.path.insert(0, _p)

from knowledge_engine.models import (  # noqa: E402
    KnowledgeAnswer,
    KnowledgeChunk,
    RetrievalResult,
)

import ke_mcp_server  # noqa: E402


def _fake_result() -> RetrievalResult:
    chunk = KnowledgeChunk(
        id="c1",
        text="some knowledge text",
        source="docs/SOME.md",
        path="docs/SOME.md",
        chunk_index=0,
        document_type="General",
    )
    return RetrievalResult(chunk=chunk, score=0.1)


def _fake_answer() -> KnowledgeAnswer:
    result = _fake_result()
    return KnowledgeAnswer(
        question="q",
        answer="the answer",
        sources=[result.chunk],
    )


class SingletonReuseTests(unittest.TestCase):
    """knowledge_search / knowledge_ask must reuse one KnowledgeService."""

    def setUp(self):
        # Each test starts from a clean, un-constructed singleton.
        ke_mcp_server._service = None

    def tearDown(self):
        ke_mcp_server._service = None

    def _make_fake_service_factory(self):
        fake_service = MagicMock()
        fake_service.search.return_value = [_fake_result()]
        fake_service.ask.return_value = _fake_answer()

        factory = MagicMock(return_value=fake_service)
        return factory, fake_service

    def test_knowledge_search_constructs_service_once_across_calls(self):
        factory, fake_service = self._make_fake_service_factory()

        with patch.object(ke_mcp_server, "create_service", factory):
            first = ke_mcp_server.knowledge_search("question one")
            second = ke_mcp_server.knowledge_search("question two")

        self.assertEqual(
            factory.call_count,
            1,
            "KnowledgeService must be constructed once and reused, "
            f"not rebuilt per call (constructed {factory.call_count} times)",
        )
        self.assertEqual(fake_service.search.call_count, 2)
        self.assertIn("some knowledge text", first)
        self.assertIn("some knowledge text", second)

    def test_knowledge_ask_constructs_service_once_across_calls(self):
        factory, fake_service = self._make_fake_service_factory()

        with patch.object(ke_mcp_server, "create_service", factory):
            first = ke_mcp_server.knowledge_ask("question one")
            second = ke_mcp_server.knowledge_ask("question two")

        self.assertEqual(
            factory.call_count,
            1,
            "KnowledgeService must be constructed once and reused, "
            f"not rebuilt per call (constructed {factory.call_count} times)",
        )
        self.assertEqual(fake_service.ask.call_count, 2)
        self.assertIn("the answer", first)
        self.assertIn("the answer", second)

    def test_search_and_ask_share_the_same_singleton(self):
        factory, fake_service = self._make_fake_service_factory()

        with patch.object(ke_mcp_server, "create_service", factory):
            ke_mcp_server.knowledge_search("q1")
            ke_mcp_server.knowledge_ask("q2")

        self.assertEqual(factory.call_count, 1)

    def test_get_service_is_idempotent(self):
        factory, fake_service = self._make_fake_service_factory()

        with patch.object(ke_mcp_server, "create_service", factory):
            first = ke_mcp_server.get_service()
            second = ke_mcp_server.get_service()

        self.assertIs(first, second)
        self.assertEqual(factory.call_count, 1)


class WarmServiceTests(unittest.TestCase):
    """warm_service() must force init eagerly and never swallow failure."""

    def setUp(self):
        ke_mcp_server._service = None

    def tearDown(self):
        ke_mcp_server._service = None

    def test_warm_service_builds_and_queries_the_singleton(self):
        fake_service = MagicMock()
        fake_service.search.return_value = [_fake_result()]
        factory = MagicMock(return_value=fake_service)

        with patch.object(ke_mcp_server, "create_service", factory):
            warmed = ke_mcp_server.warm_service()

        factory.assert_called_once()
        fake_service.search.assert_called_once()
        self.assertIs(warmed, fake_service)
        # Subsequent tool calls must reuse the already-warmed instance.
        with patch.object(ke_mcp_server, "create_service", factory):
            ke_mcp_server.knowledge_search("another question")
        factory.assert_called_once()

    def test_warm_service_failure_propagates_not_swallowed(self):
        fake_service = MagicMock()
        fake_service.search.side_effect = RuntimeError("chroma collection unreachable")
        factory = MagicMock(return_value=fake_service)

        with patch.object(ke_mcp_server, "create_service", factory):
            with self.assertRaises(RuntimeError):
                ke_mcp_server.warm_service()

    def test_warm_service_construction_failure_propagates(self):
        factory = MagicMock(side_effect=RuntimeError("cannot open db_path"))

        with patch.object(ke_mcp_server, "create_service", factory):
            with self.assertRaises(RuntimeError):
                ke_mcp_server.warm_service()


if __name__ == "__main__":
    unittest.main()
