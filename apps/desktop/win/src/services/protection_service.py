"""
Protection Service
Handles protective actions from backend
"""

import logging
from typing import List, Dict, Any, Optional

from enums import (
    ProtectiveActionType, ProtectiveActionSubject,
    get_protective_action_name, get_severity_name, get_subject_name
)

logger = logging.getLogger(__name__)


class ProtectionService:
    """
    Handles protective actions
    - Execute actions from backend
    - Map action types to cache values
    - Display notifications
    """

    def __init__(self, tray, cache):
        self.tray = tray
        self.cache = cache

        # Map backend action types to cache values
        self.action_map = {
            ProtectiveActionType.NONE: 0,
            ProtectiveActionType.DisplayNotification: 2,
            ProtectiveActionType.EmailNotification: 1,
            ProtectiveActionType.SoundAlert: 3,
            ProtectiveActionType.BlockUrl: 4,
            ProtectiveActionType.UserDisplayNotification: 2,
            ProtectiveActionType.QuarantineDevice: 4,
            ProtectiveActionType.BlockRemoteAccess: 4,
        }

    def execute_actions(
        self,
        actions: List[Dict],
        url: str,
        data: Dict[str, Any]
    ):
        """Execute list of protective actions"""
        for action in actions:
            action_type = action.get('ActionType', 0)
            subject = action.get('Subject', 0)
            message = action.get('Message', 'Security alert')
            level = action.get('Level', 1)

            action_name = get_protective_action_name(action_type)
            subject_name = get_subject_name(subject)

            print(f"[PROTECTION] {action_name} for {subject_name} - {message}")

            if subject == ProtectiveActionSubject.Device:
                self._execute_device_action(action_type, message, level, url, data)
            elif subject == ProtectiveActionSubject.User:
                self._execute_user_action(action_type, message, level, url, data)
            elif subject == ProtectiveActionSubject.Protector:
                self._execute_protector_action(action_type, message, level, url, data)

    def _execute_device_action(
        self,
        action_type: int,
        message: str,
        level: int,
        url: str,
        data: Dict[str, Any]
    ):
        """Execute device-targeted action"""

        if action_type == ProtectiveActionType.DisplayNotification:
            self.tray.set_alert(True)
            severity = get_severity_name(data.get('Severity', 1))
            # Map severity to risk level
            risk_level = "medium"
            if severity in ["Critical", "High"]:
                risk_level = "critical" if severity == "Critical" else "high"
            elif severity == "Low":
                risk_level = "low"
            self.tray.show_notification(
                title=f"AntiScam Alert - {severity}",
                message=message,
                risk_level=risk_level
            )

        elif action_type == ProtectiveActionType.SoundAlert:
            print("[PROTECTION] Playing alert sound...")
            # TODO: Implement sound alert

        elif action_type == ProtectiveActionType.BlockUrl:
            print(f"[PROTECTION] Blocking URL: {url}")
            # Cache already has the protective action

        elif action_type == ProtectiveActionType.QuarantineDevice:
            print("[PROTECTION] Quarantine device mode activated")
            self.tray.set_alert(True)
            self.tray.show_notification(
                title="Device Quarantined",
                message="Your device has been quarantined. Contact support.",
                risk_level="critical"
            )

    def _execute_user_action(
        self,
        action_type: int,
        message: str,
        level: int,
        url: str,
        data: Dict[str, Any]
    ):
        """Execute user-targeted action"""

        if action_type == ProtectiveActionType.UserDisplayNotification:
            self.tray.set_alert(True)
            severity = get_severity_name(data.get('Severity', 1))
            # Map severity to risk level
            risk_level = "medium"
            if severity in ["Critical", "High"]:
                risk_level = "critical" if severity == "Critical" else "high"
            elif severity == "Low":
                risk_level = "low"
            self.tray.show_notification(
                title=f"Security Alert - {severity}",
                message=message,
                risk_level=risk_level
            )

        elif action_type == ProtectiveActionType.EmailNotification:
            print("[PROTECTION] Sending email notification to user")
            # TODO: Implement email

    def _execute_protector_action(
        self,
        action_type: int,
        message: str,
        level: int,
        url: str,
        data: Dict[str, Any]
    ):
        """Execute protector/guardian-targeted action"""

        if action_type == ProtectiveActionType.EmailNotification:
            print("[PROTECTION] Sending email notification to protector")
            # TODO: Implement

        elif action_type == ProtectiveActionType.DisplayNotification:
            print("[PROTECTION] Logging alert for protector dashboard")

    def show_display_notification_actions(
        self,
        actions: List[Dict],
        title: str,
        risk_level: str = "critical",
        action_buttons: Optional[list] = None,
        fallback_message: Optional[str] = None,
    ) -> bool:
        """
        Run only DisplayNotification / UserDisplayNotification entries from a
        ProtectiveAction list using a custom toast title and buttons. Used by
        the ImmediateDanger flow (and any future event-driven flow) where the
        toast styling is dictated by the event, not by the URL-analysis path.

        Args:
            actions: list of action dicts from notification Data.ProtectiveActions
            title: toast title (the per-action Message goes into the body)
            risk_level: 'critical'/'high'/'medium'/'low'/'none' (drives icon+sound)
            action_buttons: list of (label, launch) tuples for toast buttons
            fallback_message: shown as a single toast when there are no
                DisplayNotification actions in `actions`. None = silent.

        Returns:
            True iff at least one toast was shown.
        """
        display_types = (
            ProtectiveActionType.DisplayNotification,
            ProtectiveActionType.UserDisplayNotification,
        )
        display_actions = [
            a for a in (actions or [])
            if a.get('ActionType') in display_types
        ]

        if not display_actions and not fallback_message:
            return False

        # Mark tray as having an alert (red icon) for non-cleared toasts
        if risk_level in ('critical', 'high', 'medium'):
            self.tray.set_alert(True)

        if display_actions:
            shown = False
            for action in display_actions:
                message = action.get('Message') or fallback_message
                if not message:
                    continue
                self.tray.show_notification(
                    title=title,
                    message=message,
                    risk_level=risk_level,
                    action_buttons=action_buttons,
                )
                shown = True
            return shown

        # No DisplayNotification actions in payload — show fallback once
        self.tray.show_notification(
            title=title,
            message=fallback_message,
            risk_level=risk_level,
            action_buttons=action_buttons,
        )
        return True

    def get_cache_action(
        self,
        protective_actions: List[Dict],
        score: int
    ) -> int:
        """
        Get protective action from server response
        Uses server's protective actions directly - no local calculations
        """

        # Use server's protective actions directly
        if protective_actions:
            max_action = ProtectiveActionType.NONE

            for action in protective_actions:
                action_type = action.get('ActionType', 0)
                subject = action.get('Subject', 0)

                # Only consider device/user actions
                if subject in [ProtectiveActionSubject.Device, ProtectiveActionSubject.User]:
                    if action_type > max_action:
                        max_action = action_type

            return self.action_map.get(max_action, 0)

        # No protective actions from server = no action
        return 0
