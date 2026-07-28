"""
Protection Service
Handles protective actions from backend
"""

import logging
from typing import List, Dict, Any, Optional

# Backend serializes SubjectKey.Type as the entity class TypeName, not the
# generic category name "Device".  All known device entity type names must map
# to ProtectiveActionSubject.Device.  "device" is included for forward-compat
# in case the backend ever sends the category name directly.
_DEVICE_TYPE_NAMES = {"personalcomputer", "smartphone", "userdevice", "device"}

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

    @staticmethod
    def _resolve_subject(action: Dict) -> int:
        """Resolve the subject of a ProtectiveAction dict.

        The backend serializes the subject as `SubjectKey` — a Key object
        `{"Type": "Device"|"User"|"Protector", "Value": "...", ...}`.
        The legacy field name `Subject` (an integer enum) is kept for
        backward-compatibility during transition.

        Resolution order:
          1. `SubjectKey.Type`  — new backend contract (primary)
          2. `Subject`          — legacy integer field (fallback)
        """
        # New contract: SubjectKey is a Key object whose Type matches the
        # subject kind.
        subject_key = action.get('SubjectKey')
        if isinstance(subject_key, dict):
            key_type = subject_key.get('Type') or subject_key.get('type') or ''
            normalized = key_type.strip().lower()
            if normalized in _DEVICE_TYPE_NAMES:
                return ProtectiveActionSubject.Device
            if normalized == 'user':
                return ProtectiveActionSubject.User
            if normalized in ('protector', 'guardian'):
                return ProtectiveActionSubject.Protector

        # Legacy fallback: integer Subject field sent by older backend versions.
        return action.get('Subject', 0)

    def execute_actions(
        self,
        actions: List[Dict],
        url: str,
        data: Dict[str, Any]
    ):
        """Execute list of protective actions"""
        for action in actions:
            action_type = action.get('ActionType', 0)
            subject = self._resolve_subject(action)
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
        details: Optional[Dict] = None,
    ) -> bool:
        """
        Render DisplayNotification / UserDisplayNotification entries from a
        ProtectiveAction list as a CENTERED, always-on-top alert window.

        Routing by risk_level:
          - 'none'  → CLEARED alert (green, with Close button). Used for
                      ImmediateDangerEnded — transforms the active locked
                      alert in place.
          - other   → LOCKED alert (red/yellow). No close button. Always
                      on top. Draggable. Persists until cleared by a
                      subsequent risk_level='none' call.

        Falls back to the Windows toast (`tray.show_notification`) only when
        the centered-alert subsystem is unavailable.

        Args:
            actions: list of action dicts from notification Data.ProtectiveActions
            title: alert title (the per-action Message goes into the body)
            risk_level: 'critical'/'high'/'medium'/'low'/'none'
            action_buttons: forwarded to the Windows-toast fallback only.
            fallback_message: shown when there are no DisplayNotification
                actions in `actions`. None = silent.

        Returns:
            True iff at least one alert was shown.
        """
        display_types = (
            ProtectiveActionType.DisplayNotification,
            ProtectiveActionType.UserDisplayNotification,
        )

        # Tolerate ActionType arriving as int OR enum-name string (e.g. "DisplayNotification")
        def _action_type(a: Dict):
            v = a.get('ActionType')
            if isinstance(v, str):
                # Match by name, case-insensitive
                for t in ProtectiveActionType:
                    if t.name.lower() == v.strip().lower():
                        return t
                try:
                    return int(v)
                except (TypeError, ValueError):
                    return None
            return v

        # Tolerate Message arriving with different casings or alternate keys
        def _message(a: Dict) -> Optional[str]:
            for k in ('Message', 'message', 'Msg', 'msg', 'Text', 'text'):
                val = a.get(k)
                if val:
                    return val
            return None

        # Diagnostic: log the raw incoming payload so we can verify what the
        # backend is sending when something looks wrong.
        if actions:
            try:
                logger.info(
                    "ProtectiveActions received (%d): %s",
                    len(actions),
                    [{'ActionType': a.get('ActionType'),
                      'Message': _message(a),
                      'SubjectKey': a.get('SubjectKey'),
                      'Subject': a.get('Subject'),  # legacy field — kept for diagnostics
                      'Level': a.get('Level')} for a in actions],
                )
            except Exception:
                pass

        display_actions = [a for a in (actions or []) if _action_type(a) in display_types]

        if not display_actions and not fallback_message:
            logger.info("show_display_notification_actions: nothing to show "
                        "(no DisplayNotification actions, no fallback)")
            return False

        # Mark tray as having an alert (red icon) for non-cleared events
        if risk_level in ('critical', 'high', 'medium'):
            self.tray.set_alert(True)

        # Build the list of (title, message) pairs to render
        pairs = []
        if display_actions:
            for action in display_actions:
                msg = _message(action) or fallback_message
                if msg:
                    pairs.append((title, msg))
        if not pairs and fallback_message:
            pairs.append((title, fallback_message))

        if not pairs:
            return False

        # Route by risk_level:
        #   'none'  → CLEARED alert (green, with Close button) — used by
        #             ImmediateDangerEnded. Transforms the existing locked
        #             window in place.
        #   other   → LOCKED alert (red/yellow, NO close button, always-on-top,
        #             draggable). Used by ImmediateDanger started/ongoing.
        # `details` (full ImmediateDanger context) is forwarded so the
        # 'View Details' button can open an in-app DangerDetailsWindow.
        shown = False
        for pair_title, pair_msg in pairs:
            if risk_level == "none":
                ok = self.tray.transform_alert_to_cleared(
                    title=pair_title, message=pair_msg,
                    details=details,
                )
            else:
                ok = self.tray.show_locked_alert(
                    title=pair_title, message=pair_msg,
                    risk_level=risk_level,
                    details=details,
                )

            if not ok:
                # Fallback to Windows toast when UI subsystem unavailable
                self.tray.show_notification(
                    title=pair_title,
                    message=pair_msg,
                    risk_level=risk_level,
                    action_buttons=action_buttons,
                )
            shown = True
        return shown

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
                subject = self._resolve_subject(action)

                # Only consider device/user actions
                if subject in [ProtectiveActionSubject.Device, ProtectiveActionSubject.User]:
                    if action_type > max_action:
                        max_action = action_type

            return self.action_map.get(max_action, 0)

        # No protective actions from server = no action
        return 0
