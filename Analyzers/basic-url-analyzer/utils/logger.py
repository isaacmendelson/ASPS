"""
Logging utilities
"""

import logging
import json
from pathlib import Path


def setup_logger(name: str = 'scam_analyzer', level: str = 'INFO') -> logging.Logger:
    """
    Setup and configure logger
    
    Args:
        name: Logger name
        level: Logging level (DEBUG, INFO, WARNING, ERROR)
    
    Returns:
        Configured logger instance
    """
    # Load settings
    config_path = Path(__file__).parent.parent / 'config' / 'settings.json'
    with open(config_path, 'r') as f:
        settings = json.load(f)
    
    logger = logging.getLogger(name)
    
    # Set level from settings or parameter
    log_level = getattr(logging, settings['logging']['level'])
    logger.setLevel(log_level)
    
    # Console handler
    if not logger.handlers:
        console_handler = logging.StreamHandler()
        console_handler.setLevel(log_level)
        
        # Format
        formatter = logging.Formatter(settings['logging']['format'])
        console_handler.setFormatter(formatter)
        
        logger.addHandler(console_handler)
    
    return logger
