"""
Tool configurations for remote access detection.

Contains detailed configuration for each supported remote access tool including:
- Process names to monitor
- Network ports
- Log file paths
- Service names
- Session indicators for log parsing
"""

import os
from typing import Optional, Dict, Any

# Tool IDs (matching config.py RemoteAccessApp)
class ToolID:
    ANYDESK = 1
    TEAMVIEWER = 2
    CHROME_REMOTE_DESKTOP = 3
    REMOTEPC = 4
    LOGMEIN = 5
    SPLASHTOP = 6
    VNC = 7
    RDP = 8
    QUICK_ASSIST = 9
    CONNECTWISE = 10


REMOTE_ACCESS_TOOLS: Dict[str, Dict[str, Any]] = {
    'anydesk': {
        'id': ToolID.ANYDESK,
        'process_names': ['anydesk.exe', 'anydesk'],
        'listen_ports': [7070],
        'log_paths': {
            'trace': r'%APPDATA%\AnyDesk\ad.trace',
            'svc_trace': r'%PROGRAMDATA%\AnyDesk\ad_svc.trace',
            'connection_trace': r'%APPDATA%\AnyDesk\connection_trace.txt'
        },
        'service_names': [],
        'session_indicators': {
            'active': ['desk.connected', 'incoming connection established', 'remote control started'],
            'inactive': ['desk.disconnected', 'session closed', 'remote control stopped']
        }
    },
    'teamviewer': {
        'id': ToolID.TEAMVIEWER,
        'process_names': ['teamviewer.exe', 'teamviewer', 'teamviewerservice.exe'],
        'listen_ports': [5938],
        'log_paths': {
            'connections_incoming': r'%PROGRAMFILES%\TeamViewer\Connections_incoming.txt',
            'connections_outgoing': r'%APPDATA%\TeamViewer\Connections.txt',
            'logfile': r'%APPDATA%\TeamViewer\TeamViewer15_Logfile.log'
        },
        'service_names': ['TeamViewer'],
        'session_indicators': {
            'active': ['connection established', 'addparticipant'],
            'inactive': ['connection closed', 'session ended']
        }
    },
    'quick_assist': {
        'id': ToolID.QUICK_ASSIST,
        'process_names': ['quickassist.exe'],
        'listen_ports': [],
        'log_paths': {},
        'service_names': [],
        'direction_detection': 'network_volume'
    },
    'logmein': {
        'id': ToolID.LOGMEIN,
        'process_names': ['logmein.exe', 'lmiguardiansvc.exe', 'logmeinrescue.exe'],
        'listen_ports': [],
        'log_paths': {},
        'service_names': ['LogMeIn'],
        'direction_detection': 'port_based'
    },
    'connectwise': {
        'id': ToolID.CONNECTWISE,
        'process_names': ['screenconnect.clientservice.exe', 'screenconnect.windowsclient.exe'],
        'listen_ports': [],
        'log_paths': {},
        'service_names': ['ScreenConnect Client'],
        'direction_detection': 'port_based'
    },
    'rustdesk': {
        'id': ToolID.VNC,
        'process_names': ['rustdesk.exe', 'rustdesk'],
        'listen_ports': [21115, 21116, 21117, 21118, 21119],
        'log_paths': {},
        'service_names': [],
        'direction_detection': 'port_based'
    },
    'chrome_remote_desktop': {
        'id': ToolID.CHROME_REMOTE_DESKTOP,
        'process_names': ['remoting_host.exe', 'chrome_remote_desktop'],
        'listen_ports': [],
        'log_paths': {},
        'service_names': [],
        'direction_detection': 'network_volume'
    }
}


def get_tool_config(app_name: str) -> Optional[Dict[str, Any]]:
    """
    Get tool configuration, expanding environment variables in paths.

    Args:
        app_name: Name of the remote access tool (e.g., 'anydesk', 'teamviewer')

    Returns:
        Tool configuration dict with expanded paths, or None if tool not found
    """
    config = REMOTE_ACCESS_TOOLS.get(app_name)
    if not config:
        return None

    # Deep copy to avoid modifying the original
    result = config.copy()

    # Expand environment variables in log paths
    if 'log_paths' in result and result['log_paths']:
        result['log_paths'] = {
            k: os.path.expandvars(os.path.expanduser(v))
            for k, v in result['log_paths'].items()
        }

    return result
