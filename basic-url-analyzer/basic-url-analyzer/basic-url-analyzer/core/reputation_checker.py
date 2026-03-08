"""
Domain reputation checker via web search
Checks how well-known a domain is by searching for mentions online
"""

import re
from pathlib import Path
from typing import Dict, List, Optional
import sys
sys.path.append(str(Path(__file__).parent.parent))

from utils.logger import setup_logger

# Try to import ddgs (new package name) or duckduckgo_search (old name)
try:
    from ddgs import DDGS
    DDGS_AVAILABLE = True
except ImportError:
    try:
        import warnings
        warnings.filterwarnings('ignore', category=RuntimeWarning, module='duckduckgo_search')
        from duckduckgo_search import DDGS
        DDGS_AVAILABLE = True
    except ImportError:
        DDGS_AVAILABLE = False
        DDGS = None


class ReputationChecker:
    """Checks domain reputation via web search mentions"""

    # Reputable source domains that indicate legitimacy
    REPUTABLE_SOURCES = [
        # News sites
        'wikipedia.org', 'reuters.com', 'bbc.com', 'cnn.com', 'nytimes.com',
        'theguardian.com', 'forbes.com', 'bloomberg.com', 'techcrunch.com',
        'wired.com', 'arstechnica.com', 'theverge.com', 'engadget.com',
        # Israeli news (for Hebrew sites)
        'ynet.co.il', 'haaretz.co.il', 'mako.co.il', 'walla.co.il',
        'globes.co.il', 'calcalist.co.il', 'israelhayom.co.il', 'kan.org.il',
        # Tech/Business
        'linkedin.com', 'crunchbase.com', 'github.com', 'stackoverflow.com',
        'trustpilot.com', 'glassdoor.com', 'indeed.com',
        # Social proof
        'twitter.com', 'x.com', 'facebook.com', 'instagram.com', 'youtube.com',
        # Government/Education
        '.gov', '.edu', '.ac.il', 'gov.il'
    ]

    # Minimum mentions from reputable sources to consider "well-known"
    MIN_REPUTABLE_MENTIONS = 2

    def __init__(self):
        """Initialize reputation checker"""
        self.logger = setup_logger('reputation_checker')

        if not DDGS_AVAILABLE:
            self.logger.warning("duckduckgo_search not installed. Run: pip install duckduckgo_search")

    def check(self, domain: str) -> Dict:
        """
        Check domain reputation by searching for mentions online.

        Args:
            domain: Domain to check (e.g., 'ynet.co.il')

        Returns:
            Dictionary with:
            - success: bool
            - is_well_known: bool - True if domain appears well-known
            - reputable_mentions: int - Count of mentions from reputable sources
            - total_results: int - Total search results found
            - mention_sources: list - Which reputable sources mentioned the domain
            - reputation_score: float - 0.0-1.0 (higher = better reputation)
            - score_adjustment: int - Points to subtract from risk score
        """
        if not DDGS_AVAILABLE:
            return {
                'success': False,
                'is_well_known': False,
                'reputable_mentions': 0,
                'total_results': 0,
                'mention_sources': [],
                'reputation_score': 0.0,
                'score_adjustment': 0,
                'error': 'duckduckgo_search not installed'
            }

        try:
            self.logger.info(f"Checking reputation for domain: {domain}")

            # Search for the domain
            search_results = self._search_domain(domain)

            if not search_results:
                self.logger.info(f"No search results found for {domain}")
                return {
                    'success': True,
                    'is_well_known': False,
                    'reputable_mentions': 0,
                    'total_results': 0,
                    'mention_sources': [],
                    'reputation_score': 0.0,
                    'score_adjustment': 0,
                    'error': ''
                }

            # Analyze results
            reputable_mentions, mention_sources = self._analyze_results(search_results, domain)
            total_results = len(search_results)

            # Calculate reputation score
            reputation_score = self._calculate_reputation_score(
                reputable_mentions,
                total_results,
                mention_sources
            )

            # Determine if well-known
            is_well_known = reputable_mentions >= self.MIN_REPUTABLE_MENTIONS

            # Calculate score adjustment (points to subtract from risk)
            score_adjustment = self._calculate_score_adjustment(reputation_score, is_well_known)

            result = {
                'success': True,
                'is_well_known': is_well_known,
                'reputable_mentions': reputable_mentions,
                'total_results': total_results,
                'mention_sources': mention_sources,
                'reputation_score': reputation_score,
                'score_adjustment': score_adjustment,
                'error': ''
            }

            self.logger.info(
                f"Reputation check for {domain}: "
                f"well_known={is_well_known}, "
                f"reputable_mentions={reputable_mentions}, "
                f"score_adjustment=-{score_adjustment}"
            )

            return result

        except Exception as e:
            self.logger.error(f"Reputation check failed for {domain}: {str(e)}")
            return {
                'success': False,
                'is_well_known': False,
                'reputable_mentions': 0,
                'total_results': 0,
                'mention_sources': [],
                'reputation_score': 0.0,
                'score_adjustment': 0,
                'error': str(e)
            }

    def _search_domain(self, domain: str, max_results: int = 15) -> List[Dict]:
        """
        Search for domain mentions using DuckDuckGo.

        Args:
            domain: Domain to search for
            max_results: Maximum results to fetch

        Returns:
            List of search result dictionaries
        """
        try:
            # Try exact match first, then broader search
            with DDGS() as ddgs:
                # First try: domain name without quotes (broader search)
                results = list(ddgs.text(f'"{domain}"', max_results=max_results))

                if not results:
                    # Fallback: search with site name (without TLD)
                    site_name = domain.split('.')[0]
                    if len(site_name) > 2:
                        results = list(ddgs.text(f"{site_name} website", max_results=max_results))

            self.logger.debug(f"Found {len(results)} search results for {domain}")
            return results

        except Exception as e:
            self.logger.warning(f"Search failed: {str(e)}")
            return []

    def _analyze_results(self, results: List[Dict], domain: str) -> tuple:
        """
        Analyze search results for reputable mentions.

        Args:
            results: List of search result dictionaries
            domain: The domain being checked (to exclude self-references)

        Returns:
            Tuple of (reputable_mentions_count, list_of_sources)
        """
        reputable_mentions = 0
        mention_sources = []

        for result in results:
            href = result.get('href', '') or result.get('link', '')
            title = result.get('title', '')
            body = result.get('body', '') or result.get('snippet', '')

            # Skip if this is the domain's own site
            if domain.lower() in href.lower():
                continue

            # Check if result is from a reputable source
            # IMPORTANT: Only count if the result actually mentions the target domain
            # Otherwise irrelevant search results from reputable sites get counted
            content = f"{title} {body}".lower()
            domain_mentioned = domain.lower() in content or domain.lower() in href.lower()

            source = self._get_reputable_source(href)
            if source and domain_mentioned:
                reputable_mentions += 1
                if source not in mention_sources:
                    mention_sources.append(source)

        return reputable_mentions, mention_sources

    def _get_reputable_source(self, url: str) -> Optional[str]:
        """
        Check if URL is from a reputable source.

        Args:
            url: URL to check

        Returns:
            Source name if reputable, None otherwise
        """
        url_lower = url.lower()

        for source in self.REPUTABLE_SOURCES:
            if source in url_lower:
                return source

        return None

    def _calculate_reputation_score(
        self,
        reputable_mentions: int,
        total_results: int,
        mention_sources: List[str]
    ) -> float:
        """
        Calculate reputation score from 0.0 to 1.0.

        Args:
            reputable_mentions: Count of reputable source mentions
            total_results: Total search results
            mention_sources: List of unique reputable sources

        Returns:
            Reputation score (0.0-1.0)
        """
        if total_results == 0:
            return 0.0

        # Base score from reputable mentions
        # Each reputable mention adds ~0.15 to score, capped at 0.75
        mention_score = min(reputable_mentions * 0.15, 0.75)

        # Bonus for diverse sources (mentioned by multiple different reputable sites)
        # Each unique source adds 0.05, capped at 0.25
        diversity_bonus = min(len(mention_sources) * 0.05, 0.25)

        # Total score capped at 1.0
        total_score = min(mention_score + diversity_bonus, 1.0)

        return round(total_score, 2)

    def _calculate_score_adjustment(self, reputation_score: float, is_well_known: bool) -> int:
        """
        Calculate how many points to subtract from risk score.

        A well-known, reputable domain should get a significant reduction.

        Args:
            reputation_score: 0.0-1.0 reputation score
            is_well_known: Whether domain meets minimum reputation threshold

        Returns:
            Points to subtract from risk score (0-40)
        """
        if not is_well_known:
            return 0

        # Scale: reputation_score 0.3-1.0 -> adjustment 10-40
        if reputation_score >= 0.8:
            return 40  # Very well-known (Wikipedia, major news)
        elif reputation_score >= 0.6:
            return 30  # Well-known (multiple reputable sources)
        elif reputation_score >= 0.4:
            return 20  # Moderately known (some reputable sources)
        elif reputation_score >= 0.2:
            return 10  # Slightly known (few mentions)
        else:
            return 0
