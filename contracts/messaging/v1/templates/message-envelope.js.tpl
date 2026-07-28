/* Generated ASPS messaging v1 binding. Do not edit. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.AspsMessagingV1 = api;
}(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  const SCHEMA_VERSION = '1.0';
  const SCHEMA_SHA256 = '__SCHEMA_SHA256__';
  const TYPES = new Set(['url_scan.request', 'url_scan.accepted', 'url_scan.result', 'url_scan.error']);
  const SOURCES = new Set(['extension', 'desktop', 'backend', 'analyzer']);
  const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
  class ContractError extends Error { constructor(code, message) { super(message); this.code = code; } }
  function canonicalizeUrl(value) {
    let url;
    try { url = new URL(value); } catch (_) { throw new ContractError('validation.invalid_canonical_url', 'URL must be absolute'); }
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password) throw new ContractError('validation.invalid_canonical_url', 'Invalid URL');
    url.hash = ''; url.hostname = url.hostname.replace(/\.$/, '').toLowerCase();
    if ((url.protocol === 'http:' && url.port === '80') || (url.protocol === 'https:' && url.port === '443')) url.port = '';
    return url.toString();
  }
  function validateEnvelope(value, requireDeviceId = false) {
    const required = ['schemaVersion','messageId','correlationId','requestId','messageType','sentAt','source','context','outcome','payload'];
    if (!value || typeof value !== 'object' || required.some(k => !Object.prototype.hasOwnProperty.call(value, k))) throw new ContractError('protocol.malformed_envelope', 'Required field missing');
    if (!/^1\.\d+$/.test(value.schemaVersion)) throw new ContractError('protocol.unsupported_schema_version', 'Only schema major 1 is supported');
    if (![value.messageId, value.correlationId, value.requestId].every(v => UUID.test(v))) throw new ContractError('protocol.invalid_identifier', 'Invalid UUID');
    if (!TYPES.has(value.messageType) || !SOURCES.has(value.source)) throw new ContractError('protocol.unsupported_discriminator', 'Unsupported discriminator');
    if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/.test(value.sentAt)) throw new ContractError('protocol.invalid_sent_at', 'Invalid sentAt');
    const c = value.context;
    if (!c || Object.keys(c).sort().join() !== 'deviceId,tabId,url') throw new ContractError('protocol.malformed_context', 'Invalid context');
    if (requireDeviceId && !c.deviceId) throw new ContractError('validation.missing_device_id', 'deviceId required');
    if (c.tabId !== null && !/^\d+$/.test(c.tabId)) throw new ContractError('validation.invalid_tab_id', 'Invalid tabId');
    if (canonicalizeUrl(c.url) !== c.url) throw new ContractError('validation.canonical_url_mismatch', 'URL not canonical');
    if (!value.payload || Array.isArray(value.payload) || typeof value.payload !== 'object') throw new ContractError('protocol.invalid_payload', 'payload must be object');
    const expected = value.messageType === 'url_scan.result' ? 'success' : value.messageType === 'url_scan.error' ? 'error' : null;
    if (expected === null && value.outcome !== null) throw new ContractError('protocol.invalid_outcome', 'Outcome must be null');
    if (expected && (!value.outcome || value.outcome.status !== expected)) throw new ContractError('protocol.invalid_outcome', 'Outcome mismatch');
    return value;
  }
  function createEnvelope(messageType, source, context, payload, ids = {}, outcome = null) {
    const uuid = () => crypto.randomUUID();
    return validateEnvelope({schemaVersion: SCHEMA_VERSION, messageId: uuid(), correlationId: ids.correlationId || uuid(), requestId: ids.requestId || uuid(), messageType, sentAt: new Date().toISOString(), source, context, outcome, payload});
  }
  return {SCHEMA_VERSION, SCHEMA_SHA256, ContractError, canonicalizeUrl, validateEnvelope, createEnvelope};
}));
