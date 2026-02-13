"""Detection submodule for remote access monitoring."""
from .tools import REMOTE_ACCESS_TOOLS, get_tool_config
from .log_parsers import parse_tool_logs
from .confidence import calculate_confidence, Confidence
from .direction import Direction, detect_direction
from .geolocation import GeoLocator, get_geolocator
