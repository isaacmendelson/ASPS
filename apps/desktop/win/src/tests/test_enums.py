"""
Regression test: the Python `Severity` enum must match the C# backend's
source-of-truth enum (ASPSBackend14_J/Common/Enums/Enumerations.cs:119):

    public enum Severity
    {
        Unknown = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

Before this fix, the Python side was missing `Unknown` and every other
member was shifted down by one (Low=0, Medium=1, High=2, Critical=3),
so any integer Severity value sent by the backend decoded to the wrong
name on the desktop agent (e.g. backend's High=3 decoded as Critical).
"""

import os
import sys
import unittest

SRC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if SRC_DIR not in sys.path:
    sys.path.insert(0, SRC_DIR)

from enums import Severity, get_severity_name  # noqa: E402


class TestSeverityEnumMatchesBackend(unittest.TestCase):
    def test_values_match_csharp_backend(self):
        self.assertEqual(Severity.Unknown, 0)
        self.assertEqual(Severity.Low, 1)
        self.assertEqual(Severity.Medium, 2)
        self.assertEqual(Severity.High, 3)
        self.assertEqual(Severity.Critical, 4)

    def test_member_count_matches_backend(self):
        self.assertEqual(len(Severity), 5)

    def test_get_severity_name_decodes_backend_wire_values(self):
        self.assertEqual(get_severity_name(0), "Unknown")
        self.assertEqual(get_severity_name(1), "Low")
        self.assertEqual(get_severity_name(2), "Medium")
        self.assertEqual(get_severity_name(3), "High")
        self.assertEqual(get_severity_name(4), "Critical")


if __name__ == "__main__":
    unittest.main(verbosity=2)
