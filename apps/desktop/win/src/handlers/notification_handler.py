"""
Notification Handler
Handles notifications from backend (via ZMQ PUB/SUB)
"""

import asyncio
import logging
from typing import Dict, Any, Optional

from services.scan_service import ScanService

logger = logging.getLogger(__name__)


class NotificationHandler:
    """
    Handles notifications from backend server
    - Process analysis results
    - Update cache
    - Execute protective actions
    - Broadcast results to extension
    """

    def __init__(self, protection_service, cache, extension_server=None):
        self.protection_service = protection_service
        self.cache = cache
        self.extension_server = extension_server
        self._event_loop: Optional[asyncio.AbstractEventLoop] = None

    def set_extension_server(self, extension_server):
        """Set extension server and capture the event loop for cross-thread broadcasting"""
        self.extension_server = extension_server
        try:
            self._event_loop = asyncio.get_running_loop()
            print("[NOTIFICATION] Extension server set + event loop captured")
        except RuntimeError:
            print("[NOTIFICATION] WARNING: No running event loop when setting extension server")

    def handle(self, notification: Dict[str, Any]):
        """Handle notification from backend"""
        print("\n" + "!" * 60)
        print("[NOTIFICATION] RECEIVED FROM SERVER!")
        print("!" * 60)

        # Backend wraps data in 'Data' object
        data = notification.get('Data', {})

        print(f"[NOTIFICATION] Alert Type: {data.get('AlertType', 'N/A')}")
        print(f"[NOTIFICATION] Severity: {data.get('Severity', 'N/A')}")

        # Extract analysis result
        analysis = self._extract_analysis(data)

        # Display indicators
        self._display_indicators(data.get('Indicators', []))

        # Execute protective actions
        protective_actions = data.get('ProtectiveActions', [])
        if protective_actions:
            print(f"[NOTIFICATION] Processing {len(protective_actions)} protective actions...")
            self.protection_service.execute_actions(
                protective_actions,
                analysis['url'],
                data
            )

        # Update cache and broadcast to extension
        if analysis['url'] and analysis['risk_score'] is not None:
            cache_data = self._update_cache(analysis, protective_actions)

            # Broadcast result to extension (cross-thread safe, with retry)
            if self.extension_server and self._event_loop and cache_data:
                broadcast_success = False
                for attempt in range(2):
                    try:
                        future = asyncio.run_coroutine_threadsafe(
                            self._broadcast_to_extension(analysis, cache_data),
                            self._event_loop
                        )
                        future.result(timeout=5)
                        broadcast_success = True
                        break
                    except Exception as e:
                        if attempt == 0:
                            print(f"[NOTIFICATION] WARNING: Broadcast attempt {attempt + 1} failed: {e}, retrying...")
                        else:
                            print(f"[NOTIFICATION] ERROR: Broadcast failed after {attempt + 1} attempts: {e}")
                            logger.error(f"Broadcast failed after retry: {e}")
                if not broadcast_success:
                    print("[NOTIFICATION] ERROR: Could not deliver result to extension")
            elif not self.extension_server:
                print("[NOTIFICATION] WARNING: No extension server - cannot broadcast")
            elif not self._event_loop:
                print("[NOTIFICATION] WARNING: No event loop - cannot broadcast")

            # Clear this specific URL from pending set
            ScanService.clear_pending_url(analysis['url'])

        print("!" * 60 + "\n")

    async def _broadcast_to_extension(self, analysis, cache_data):
        """Broadcast URL result to extension"""
        try:
            result_message = {
                'type': 'url_result',
                'url': analysis['url'],
                'score': cache_data['score'],
                'riskType': cache_data['risk_types'],
                'protectiveAction': cache_data['protective_action'],
                'fromCache': False
            }
            await self.extension_server.broadcast(result_message)
            print(f"[NOTIFICATION] Broadcasted result to extension: score={cache_data['score']}")
        except Exception as e:
            logger.error(f"Error broadcasting to extension: {e}")
            raise  # Re-raise so caller's retry loop can detect failure

    def _extract_analysis(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Extract analysis result from notification data"""
        analysis_result = data.get('AnalysisResult', {})
        analyzer_results = data.get('AnalyzerResults', {})

        url = None
        risk_score = None
        risk_assessment = {}

        if analysis_result and isinstance(analysis_result, dict):
            print(f"[NOTIFICATION] Type: {analysis_result.get('TypeName', 'N/A')}")
            url = analysis_result.get('Url')

            risk_assessment = analysis_result.get('risk_assessment', {})
            risk_score = risk_assessment.get('risk_score')

            print(f"[NOTIFICATION] URL: {url}")
            print(f"[NOTIFICATION] Domain: {analysis_result.get('Domain', 'N/A')}")
            print(f"[NOTIFICATION] Analysis Time: {analysis_result.get('analysis_time_ms', 'N/A')}ms")
            print(f"[NOTIFICATION] Risk Score: {risk_score}")
            print(f"[NOTIFICATION] Risk Level: {risk_assessment.get('risk_level', 'N/A')}")
            print(f"[NOTIFICATION] Is Scam: {risk_assessment.get('is_scam', False)}")

        # Fallback: try RiskAssessment at Data level
        if not risk_score:
            risk_assessment = data.get('RiskAssessment', {})
            risk_score = risk_assessment.get('risk_score')
            print(f"[NOTIFICATION] Risk Score (fallback): {risk_score}")

        # Fallback: try AnalyzerResults
        if not url and analyzer_results:
            url = analyzer_results.get('Url')
            if url:
                print(f"[NOTIFICATION] URL (from AnalyzerResults): {url}")

        # Fallback: use pending URL from ScanService
        if not url:
            url = ScanService.get_pending_url()
            if url:
                print(f"[NOTIFICATION] URL (from pending scan): {url}")

        # Get phishing check
        phishing_check = None
        if analysis_result:
            phishing_check = analysis_result.get('phishing_check', {})
        elif analyzer_results:
            phishing_check = analyzer_results.get('phishing_check', {})

        return {
            'url': url,
            'risk_score': risk_score,
            'risk_assessment': risk_assessment,
            'phishing_check': phishing_check
        }

    def _display_indicators(self, indicators: list):
        """Display indicators from notification"""
        if not indicators:
            return

        print(f"[NOTIFICATION] Indicators: {len(indicators)} found")
        for idx, indicator in enumerate(indicators, 1):
            if isinstance(indicator, dict):
                ind_type = indicator.get('IndicatorType', indicator.get('$type', 'N/A'))
                ind_value = indicator.get('Value', 'N/A')
                print(f"[NOTIFICATION]    {idx}. Type: {ind_type}, Value: {ind_value}")
            else:
                print(f"[NOTIFICATION]    {idx}. {indicator}")

    def _update_cache(self, analysis: Dict[str, Any], protective_actions: list) -> Dict[str, Any]:
        """Update cache with analysis results - using server score directly"""
        url = analysis['url']
        risk_score = analysis['risk_score']
        risk_assessment = analysis['risk_assessment']
        phishing_check = analysis.get('phishing_check', {})

        # Use server score directly - no conversion
        score = int(risk_score)

        # Get risk types from server
        risk_types = []
        if risk_assessment.get('is_scam'):
            risk_types.append('Scam')
        if phishing_check and phishing_check.get('Is_known_phishing'):
            risk_types.append('Phishing')

        # Add risk_level from server if available
        risk_level = risk_assessment.get('risk_level')
        if risk_level and risk_level not in risk_types:
            risk_types.append(risk_level)

        # Get protective action from server response
        protective_action = self.protection_service.get_cache_action(
            protective_actions,
            score
        )

        print(f"[NOTIFICATION] Updating cache: score={score} (from server), "
              f"risks={risk_types}, action={protective_action}")

        self.cache.set(url, score, risk_types, protective_action, ttl=3600)

        return {
            'score': score,
            'risk_types': risk_types,
            'protective_action': protective_action
        }
