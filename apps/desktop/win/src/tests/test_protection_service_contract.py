"""
Unit tests for ProtectionService — SubjectKey / Subject contract alignment.

Covers ASPS-618 (DT-4 finding):
  - Backend serializes the subject as `SubjectKey` (a Key object with Type/Value/InstanceName).
  - Legacy field name `Subject` (integer enum) is supported as a fallback.
  - All dispatch paths (execute_actions, get_cache_action) must use the same
    resolution logic.
"""

import sys
import os
import unittest
from unittest.mock import MagicMock, patch, call

# Allow imports from the src directory
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from services.protection_service import ProtectionService
from enums import ProtectiveActionSubject, ProtectiveActionType


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_service():
    tray = MagicMock()
    cache = MagicMock()
    return ProtectionService(tray=tray, cache=cache)


def _subject_key(type_name: str, value: str = "123") -> dict:
    """Build a SubjectKey dict as the backend serializes it."""
    return {"Type": type_name, "Value": value}


# ---------------------------------------------------------------------------
# _resolve_subject — unit tests for the resolution helper
# ---------------------------------------------------------------------------

class TestResolveSubject(unittest.TestCase):
    """Tests for the ProtectionService._resolve_subject static method."""

    # --- New contract: SubjectKey — real Backend entity type names ---
    # The Backend serializes SubjectKey.Type as the entity class TypeName
    # (PersonalComputer, SmartPhone, UserDevice), NOT the generic category
    # name "Device".  All three must resolve to ProtectiveActionSubject.Device.

    def test_subject_key_personal_computer_resolves_to_device(self):
        """PersonalComputer is the TypeName used by PersonalComputer entities."""
        action = {'SubjectKey': _subject_key('PersonalComputer')}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Device)

    def test_subject_key_smartphone_resolves_to_device(self):
        """SmartPhone is the TypeName used by SmartPhone entities."""
        action = {'SubjectKey': _subject_key('SmartPhone')}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Device)

    def test_subject_key_userdevice_resolves_to_device(self):
        """UserDevice is used by DeviceAlerts and ProtectiveActionsFactory."""
        action = {'SubjectKey': _subject_key('UserDevice')}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Device)

    def test_subject_key_device_resolves_to_device_backward_compat(self):
        """'Device' is kept in the set for forward-compat if the backend ever
        sends the generic category name directly."""
        action = {'SubjectKey': _subject_key('Device')}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Device)

    def test_subject_key_user_resolves_to_user(self):
        action = {'SubjectKey': _subject_key('User')}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.User)

    def test_subject_key_protector_resolves_to_protector(self):
        action = {'SubjectKey': _subject_key('Protector')}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Protector)

    def test_subject_key_guardian_resolves_to_protector(self):
        """'Guardian' is an accepted alias for Protector."""
        action = {'SubjectKey': _subject_key('Guardian')}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Protector)

    def test_subject_key_type_is_case_insensitive(self):
        """All device type names are matched case-insensitively."""
        device_variants = [
            'personalcomputer', 'PersonalComputer', 'PERSONALCOMPUTER',
            'smartphone', 'SmartPhone', 'SMARTPHONE',
            'userdevice', 'UserDevice', 'USERDEVICE',
            'device', 'DEVICE', 'Device',
        ]
        for variant in device_variants:
            with self.subTest(variant=variant):
                action = {'SubjectKey': {'Type': variant, 'Value': '1'}}
                result = ProtectionService._resolve_subject(action)
                self.assertEqual(result, ProtectiveActionSubject.Device)

    def test_subject_key_unknown_type_falls_through_to_legacy(self):
        """An unknown SubjectKey.Type (not a device entity name) should fall
        through to the Subject integer field."""
        action = {'SubjectKey': _subject_key('Unknown'), 'Subject': int(ProtectiveActionSubject.User)}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.User)

    def test_subject_key_empty_type_falls_through_to_legacy(self):
        action = {'SubjectKey': {'Type': '', 'Value': '1'}, 'Subject': int(ProtectiveActionSubject.Device)}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Device)

    def test_subject_key_missing_type_falls_through_to_legacy(self):
        """SubjectKey present but no Type key — fall back to Subject."""
        action = {'SubjectKey': {'Value': '42'}, 'Subject': int(ProtectiveActionSubject.Protector)}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Protector)

    def test_subject_key_with_instance_name(self):
        """InstanceName should not affect resolution; use real entity type names."""
        for type_name in ('PersonalComputer', 'SmartPhone', 'UserDevice'):
            with self.subTest(type_name=type_name):
                action = {'SubjectKey': {'Type': type_name, 'Value': '7', 'InstanceName': 'pc01'}}
                result = ProtectionService._resolve_subject(action)
                self.assertEqual(result, ProtectiveActionSubject.Device)

    # --- Legacy fallback: Subject integer ---

    def test_legacy_subject_integer_device(self):
        action = {'Subject': int(ProtectiveActionSubject.Device)}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Device)

    def test_legacy_subject_integer_user(self):
        action = {'Subject': int(ProtectiveActionSubject.User)}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.User)

    def test_legacy_subject_integer_protector(self):
        action = {'Subject': int(ProtectiveActionSubject.Protector)}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Protector)

    def test_no_subject_fields_defaults_to_none(self):
        """When neither SubjectKey nor Subject is present, result is NONE (0)."""
        action = {'ActionType': 1, 'Message': 'Test'}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.NONE)

    def test_subject_key_takes_precedence_over_subject(self):
        """SubjectKey.Type should win over the legacy Subject integer."""
        action = {
            'SubjectKey': _subject_key('Device'),  # => Device
            'Subject': int(ProtectiveActionSubject.User),  # contradicts — must be ignored
        }
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Device)

    def test_subject_key_non_dict_falls_through_to_legacy(self):
        """SubjectKey that is not a dict is ignored; fall back to Subject."""
        action = {'SubjectKey': "Device", 'Subject': int(ProtectiveActionSubject.User)}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.User)

    def test_subject_key_none_falls_through_to_legacy(self):
        action = {'SubjectKey': None, 'Subject': int(ProtectiveActionSubject.Device)}
        result = ProtectionService._resolve_subject(action)
        self.assertEqual(result, ProtectiveActionSubject.Device)


# ---------------------------------------------------------------------------
# execute_actions — dispatch routing via SubjectKey
# ---------------------------------------------------------------------------

class TestExecuteActionsDispatch(unittest.TestCase):
    """Verify that execute_actions routes to the correct _execute_* method
    using SubjectKey (new contract) and Subject (legacy fallback)."""

    def setUp(self):
        self.svc = _make_service()

    # -- New contract routing --

    def test_new_contract_device_routes_to_device_action(self):
        """All real backend device entity type names must route to device action."""
        for type_name in ('PersonalComputer', 'SmartPhone', 'UserDevice'):
            with self.subTest(type_name=type_name):
                svc = _make_service()
                action = {
                    'SubjectKey': _subject_key(type_name),
                    'ActionType': int(ProtectiveActionType.DisplayNotification),
                    'Message': 'Test',
                    'Level': 1,
                }
                with patch.object(svc, '_execute_device_action') as mock_device, \
                     patch.object(svc, '_execute_user_action') as mock_user, \
                     patch.object(svc, '_execute_protector_action') as mock_protector:
                    svc.execute_actions([action], 'http://example.com', {})
                    mock_device.assert_called_once()
                    mock_user.assert_not_called()
                    mock_protector.assert_not_called()

    def test_new_contract_user_routes_to_user_action(self):
        action = {
            'SubjectKey': _subject_key('User'),
            'ActionType': int(ProtectiveActionType.UserDisplayNotification),
            'Message': 'Test',
            'Level': 1,
        }
        with patch.object(self.svc, '_execute_device_action') as mock_device, \
             patch.object(self.svc, '_execute_user_action') as mock_user, \
             patch.object(self.svc, '_execute_protector_action') as mock_protector:
            self.svc.execute_actions([action], 'http://example.com', {})
            mock_user.assert_called_once()
            mock_device.assert_not_called()
            mock_protector.assert_not_called()

    def test_new_contract_protector_routes_to_protector_action(self):
        action = {
            'SubjectKey': _subject_key('Protector'),
            'ActionType': int(ProtectiveActionType.EmailNotification),
            'Message': 'Test',
            'Level': 1,
        }
        with patch.object(self.svc, '_execute_device_action') as mock_device, \
             patch.object(self.svc, '_execute_user_action') as mock_user, \
             patch.object(self.svc, '_execute_protector_action') as mock_protector:
            self.svc.execute_actions([action], 'http://example.com', {})
            mock_protector.assert_called_once()
            mock_device.assert_not_called()
            mock_user.assert_not_called()

    # -- Legacy Subject fallback routing --

    def test_legacy_subject_device_routes_to_device_action(self):
        action = {
            'Subject': int(ProtectiveActionSubject.Device),
            'ActionType': int(ProtectiveActionType.DisplayNotification),
            'Message': 'Test',
            'Level': 1,
        }
        with patch.object(self.svc, '_execute_device_action') as mock_device, \
             patch.object(self.svc, '_execute_user_action') as mock_user:
            self.svc.execute_actions([action], 'http://example.com', {})
            mock_device.assert_called_once()
            mock_user.assert_not_called()

    def test_legacy_subject_user_routes_to_user_action(self):
        action = {
            'Subject': int(ProtectiveActionSubject.User),
            'ActionType': int(ProtectiveActionType.UserDisplayNotification),
            'Message': 'Test',
            'Level': 1,
        }
        with patch.object(self.svc, '_execute_user_action') as mock_user, \
             patch.object(self.svc, '_execute_device_action') as mock_device:
            self.svc.execute_actions([action], 'http://example.com', {})
            mock_user.assert_called_once()
            mock_device.assert_not_called()

    def test_missing_subject_fields_no_dispatch(self):
        """Action with neither SubjectKey nor Subject should not dispatch to any handler."""
        action = {
            'ActionType': int(ProtectiveActionType.DisplayNotification),
            'Message': 'Test',
            'Level': 1,
        }
        with patch.object(self.svc, '_execute_device_action') as mock_device, \
             patch.object(self.svc, '_execute_user_action') as mock_user, \
             patch.object(self.svc, '_execute_protector_action') as mock_protector:
            self.svc.execute_actions([action], 'http://example.com', {})
            mock_device.assert_not_called()
            mock_user.assert_not_called()
            mock_protector.assert_not_called()

    def test_old_bug_subject_0_no_longer_dispatches_with_new_backend(self):
        """Before the fix, a payload with only SubjectKey (no Subject) would
        default subject to 0 (NONE) and silently skip dispatch. Verify this is
        fixed: all real backend device type names must dispatch to
        _execute_device_action."""
        for type_name in ('PersonalComputer', 'SmartPhone', 'UserDevice'):
            with self.subTest(type_name=type_name):
                svc = _make_service()
                action = {
                    'SubjectKey': _subject_key(type_name),
                    # No 'Subject' field — exactly what the new backend sends
                    'ActionType': int(ProtectiveActionType.DisplayNotification),
                    'Message': 'Alert',
                    'Level': 2,
                }
                with patch.object(svc, '_execute_device_action') as mock_device:
                    svc.execute_actions([action], 'http://scam.example', {})
                    # Must have dispatched — the pre-fix bug would have skipped this
                    mock_device.assert_called_once()

    def test_multiple_actions_each_dispatched_correctly(self):
        """All actions in the list are processed independently.
        Device action uses a real backend entity type name (PersonalComputer)."""
        actions = [
            {'SubjectKey': _subject_key('PersonalComputer'), 'ActionType': 1, 'Message': 'A', 'Level': 1},
            {'SubjectKey': _subject_key('User'), 'ActionType': 5, 'Message': 'B', 'Level': 1},
            {'Subject': int(ProtectiveActionSubject.Protector), 'ActionType': 2, 'Message': 'C', 'Level': 1},
        ]
        with patch.object(self.svc, '_execute_device_action') as mock_device, \
             patch.object(self.svc, '_execute_user_action') as mock_user, \
             patch.object(self.svc, '_execute_protector_action') as mock_protector:
            self.svc.execute_actions(actions, 'http://example.com', {})
            self.assertEqual(mock_device.call_count, 1)
            self.assertEqual(mock_user.call_count, 1)
            self.assertEqual(mock_protector.call_count, 1)


# ---------------------------------------------------------------------------
# get_cache_action — subject resolution
# ---------------------------------------------------------------------------

class TestGetCacheActionSubjectResolution(unittest.TestCase):
    """Verify get_cache_action filters by subject using _resolve_subject."""

    def setUp(self):
        self.svc = _make_service()

    def test_device_subject_key_included_in_max_calculation(self):
        """All real backend device entity type names are included in cache
        action calculation."""
        for type_name in ('PersonalComputer', 'SmartPhone', 'UserDevice'):
            with self.subTest(type_name=type_name):
                svc = _make_service()
                actions = [
                    {'SubjectKey': _subject_key(type_name), 'ActionType': int(ProtectiveActionType.BlockUrl)},
                ]
                result = svc.get_cache_action(actions, score=80)
                # BlockUrl => cache value 4
                self.assertEqual(result, 4)

    def test_user_subject_key_included_in_max_calculation(self):
        actions = [
            {'SubjectKey': _subject_key('User'), 'ActionType': int(ProtectiveActionType.DisplayNotification)},
        ]
        result = self.svc.get_cache_action(actions, score=50)
        # DisplayNotification => cache value 2
        self.assertEqual(result, 2)

    def test_protector_subject_key_excluded_from_max_calculation(self):
        """Protector actions should NOT affect the cache action value."""
        actions = [
            {'SubjectKey': _subject_key('Protector'), 'ActionType': int(ProtectiveActionType.BlockUrl)},
        ]
        result = self.svc.get_cache_action(actions, score=80)
        # Protector is excluded, so result should be 0 (NONE)
        self.assertEqual(result, 0)

    def test_old_bug_subject_key_without_legacy_field_was_ignored(self):
        """Before fix: SubjectKey-only payload defaulted subject to 0 (NONE)
        and was excluded from max calculation. Now it must be included.
        Uses real backend entity type names."""
        for type_name in ('PersonalComputer', 'SmartPhone', 'UserDevice'):
            with self.subTest(type_name=type_name):
                svc = _make_service()
                actions = [
                    {
                        'SubjectKey': _subject_key(type_name),
                        # No 'Subject' key at all — new backend contract
                        'ActionType': int(ProtectiveActionType.DisplayNotification),
                    },
                ]
                result = svc.get_cache_action(actions, score=60)
                # Post-fix: Device action IS included; DisplayNotification => 2
                self.assertGreater(result, 0)

    def test_legacy_subject_integer_device_included(self):
        actions = [
            {'Subject': int(ProtectiveActionSubject.Device), 'ActionType': int(ProtectiveActionType.SoundAlert)},
        ]
        result = self.svc.get_cache_action(actions, score=70)
        # SoundAlert => cache value 3
        self.assertEqual(result, 3)

    def test_subject_key_takes_precedence_in_cache_action(self):
        """When SubjectKey says a device entity type and legacy Subject says
        Protector (excluded), SubjectKey wins and the action IS included."""
        actions = [
            {
                'SubjectKey': _subject_key('PersonalComputer'),
                'Subject': int(ProtectiveActionSubject.Protector),  # contradicts
                'ActionType': int(ProtectiveActionType.DisplayNotification),
            },
        ]
        result = self.svc.get_cache_action(actions, score=50)
        # Device wins => DisplayNotification => 2
        self.assertEqual(result, 2)

    def test_empty_actions_returns_zero(self):
        result = self.svc.get_cache_action([], score=50)
        self.assertEqual(result, 0)

    def test_none_actions_returns_zero(self):
        result = self.svc.get_cache_action(None, score=50)
        self.assertEqual(result, 0)

    def test_max_across_mixed_subjects(self):
        """Max is computed across all Device+User actions.
        Device uses SmartPhone (real backend entity type name)."""
        actions = [
            {'SubjectKey': _subject_key('SmartPhone'), 'ActionType': int(ProtectiveActionType.DisplayNotification)},  # => 2
            {'SubjectKey': _subject_key('User'),        'ActionType': int(ProtectiveActionType.BlockUrl)},              # => 4
            {'SubjectKey': _subject_key('Protector'),   'ActionType': int(ProtectiveActionType.QuarantineDevice)},     # excluded
        ]
        result = self.svc.get_cache_action(actions, score=90)
        # Max of Device(2) and User(4) is 4
        self.assertEqual(result, 4)


if __name__ == '__main__':
    unittest.main(verbosity=2)
