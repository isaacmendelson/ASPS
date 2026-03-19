// Minimal test to isolate import issues
console.log('[Test] Starting import test...');

// Test imports one by one
try {
  console.log('[Test] Importing StateManager...');
} catch (e) {
  console.error('[Test] StateManager failed:', e);
}

import { stateManager } from './state/StateManager.js';
console.log('[Test] ✓ StateManager imported');

import { messageBus, MSG } from './messaging/index.js';
console.log('[Test] ✓ messageBus imported');

import { REMOTE_TOOL_NAMES, REMOTE_TOOL } from './messaging/MessageTypes.js';
console.log('[Test] ✓ MessageTypes imported');

import { connectionService } from './services/ConnectionService.js';
console.log('[Test] ✓ ConnectionService imported');

import { cacheService } from './services/CacheService.js';
console.log('[Test] ✓ CacheService imported');

import { scanService } from './services/ScanService.js';
console.log('[Test] ✓ ScanService imported');

import { protectionService } from './services/ProtectionService.js';
console.log('[Test] ✓ ProtectionService imported');

import { iconService } from './services/IconService.js';
console.log('[Test] ✓ IconService imported');

import { authService } from './services/AuthService.js';
console.log('[Test] ✓ AuthService imported');

import { messageQueueService } from './services/MessageQueueService.js';
console.log('[Test] ✓ MessageQueueService imported');

console.log('[Test] All imports successful!');

// Minimal init
chrome.runtime.onInstalled.addListener(() => {
  console.log('[Test] Extension installed');
});
