using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ChatBridgeService.Controllers;

[ApiController]
public class AgentController : ControllerBase
{
    private readonly IInstanceService _instances;
    private readonly ICreatioForwarder _creatio;
    private readonly ILogger<AgentController> _logger;

    public AgentController(IInstanceService instances, ICreatioForwarder creatio, ILogger<AgentController> logger)
    {
        _instances = instances;
        _creatio   = creatio;
        _logger    = logger;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("agent/{apiKey}/{contactId}")]
    public async Task<IActionResult> Dashboard(string apiKey, string contactId, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return NotFound("Instance not found");
        return Content(BuildDashboardHtml(apiKey, contactId), "text/html; charset=utf-8");
    }

    // ── API: heartbeat ────────────────────────────────────────────────────────

    [HttpPost("api/{apiKey}/agent/heartbeat")]
    public async Task<IActionResult> Heartbeat(string apiKey, [FromBody] AgentActionDto body, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return Ok(new { success = false, error = "Instance not found" });
        try
        {
            await _creatio.HeartbeatAsync(instance, body.ContactId, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat error");
            return Ok(new { success = false, error = ex.Message });
        }
    }

    // ── API: set online / offline ─────────────────────────────────────────────

    [HttpPost("api/{apiKey}/agent/online")]
    public async Task<IActionResult> SetOnline(string apiKey, [FromBody] SetOnlineDto body, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return Ok(new { success = false, error = "Instance not found" });
        try
        {
            await _creatio.SetOnlineAsync(instance, body.ContactId, body.IsOnline, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetOnline error");
            return Ok(new { success = false, error = ex.Message });
        }
    }

    // ── API: manual assign phone to agent queue ───────────────────────────────

    [HttpPost("api/{apiKey}/agent/request")]
    public async Task<IActionResult> RequestAgent(string apiKey, [FromBody] RequestAgentDto body, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return Ok(new { success = false, error = "Instance not found" });
        try
        {
            await _creatio.RequestAgentAsync(instance, body.PhoneNumber, body.CustomerName ?? body.PhoneNumber, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RequestAgent error");
            return Ok(new { success = false, error = ex.Message });
        }
    }

    // ── API: get my chats (active + queue) ────────────────────────────────────

    [HttpPost("api/{apiKey}/agent/chats")]
    public async Task<IActionResult> GetMyChats(string apiKey, [FromBody] AgentActionDto body, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return Ok(new { success = false, error = "Instance not found" });
        try
        {
            var json = await _creatio.GetMyChatsAsync(instance, body.ContactId, ct);
            try { JsonDocument.Parse(json); return Content(json, "application/json"); }
            catch { return Ok(new { success = false, error = "Creatio error" }); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetMyChats error");
            return Ok(new { success = false, error = ex.Message });
        }
    }

    // ── API: close chat ───────────────────────────────────────────────────────

    [HttpPost("api/{apiKey}/close")]
    public async Task<IActionResult> CloseChat(string apiKey, [FromBody] CloseChatDto body, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return Ok(new { success = false, error = "Instance not found" });
        try
        {
            var json = await _creatio.CloseChatAsync(instance, body.AgentChatId, body.ContactId, ct);
            try { JsonDocument.Parse(json); return Content(json, "application/json"); }
            catch { return Ok(new { success = false, error = "Creatio error" }); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CloseChat error");
            return Ok(new { success = false, error = ex.Message });
        }
    }

    // ── Dashboard HTML ────────────────────────────────────────────────────────

    private static string BuildDashboardHtml(string apiKey, string contactId)
    {
        var safeKey       = JsonSerializer.Serialize(apiKey);
        var safeContactId = JsonSerializer.Serialize(contactId);

        return $$"""
<!DOCTYPE html>
<html lang="id">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>ChatBridge Agent</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f0f2f5;min-height:100vh}
#header{background:#075E54;color:#fff;padding:12px 20px;display:flex;align-items:center;justify-content:space-between;position:sticky;top:0;z-index:10}
#header .left{display:flex;align-items:center;gap:10px}
#header .avatar{width:36px;height:36px;border-radius:50%;background:#25D366;display:flex;align-items:center;justify-content:center;font-size:16px;flex-shrink:0}
#agent-name{font-size:15px;font-weight:700}
#agent-sub{font-size:11px;opacity:.75;margin-top:1px}
.online-toggle{display:flex;align-items:center;gap:8px;cursor:pointer;user-select:none}
.toggle-label{font-size:12px;font-weight:600}
.toggle-switch{position:relative;width:40px;height:22px}
.toggle-switch input{opacity:0;width:0;height:0}
.toggle-slider{position:absolute;inset:0;background:#ccc;border-radius:22px;transition:.3s}
.toggle-slider:before{content:'';position:absolute;width:16px;height:16px;left:3px;bottom:3px;background:#fff;border-radius:50%;transition:.3s}
input:checked+.toggle-slider{background:#25D366}
input:checked+.toggle-slider:before{transform:translateX(18px)}
.page{max-width:900px;margin:0 auto;padding:16px}
.section-title{font-size:13px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:.5px;margin:16px 0 8px}
.card-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:12px}
.chat-card{background:#fff;border-radius:10px;padding:14px 16px;box-shadow:0 1px 3px rgba(0,0,0,.08)}
.chat-card .cname{font-size:14px;font-weight:700;color:#1e1e2e;margin-bottom:2px}
.chat-card .phone{font-size:12px;color:#64748b;margin-bottom:8px}
.chat-card .time{font-size:11px;color:#94a3b8}
.chat-card .actions{display:flex;gap:6px;margin-top:10px}
.btn{padding:7px 14px;border-radius:8px;font-size:12px;font-weight:700;cursor:pointer;border:none;text-decoration:none;display:inline-flex;align-items:center}
.btn-open{background:#075E54;color:#fff}
.btn-close{background:#fee2e2;color:#dc2626}
.btn:hover{opacity:.85}
.queue-card{background:#fff;border-radius:10px;padding:12px 16px;box-shadow:0 1px 3px rgba(0,0,0,.08);display:flex;align-items:center;justify-content:space-between;margin-bottom:8px}
.queue-pos{width:32px;height:32px;border-radius:50%;background:#7c3aed;color:#fff;font-size:13px;font-weight:700;display:flex;align-items:center;justify-content:center;flex-shrink:0}
.queue-info{flex:1;margin:0 12px}
.queue-info .qname{font-size:13px;font-weight:700;color:#1e1e2e}
.queue-info .qphone{font-size:12px;color:#64748b}
.empty{text-align:center;color:#94a3b8;padding:32px;font-size:13px;background:#fff;border-radius:10px}
#refresh-status{font-size:11px;color:#94a3b8;text-align:center;padding:8px 0}
</style>
</head>
<body>
<div id="header">
  <div class="left">
    <div class="avatar">👤</div>
    <div>
      <div id="agent-name">Loading...</div>
      <div id="agent-sub">ChatBridge Agent</div>
    </div>
  </div>
  <label class="online-toggle" title="Toggle online/offline">
    <span class="toggle-label" id="online-label">Offline</span>
    <div class="toggle-switch">
      <input type="checkbox" id="online-toggle" onchange="toggleOnline(this.checked)">
      <span class="toggle-slider"></span>
    </div>
  </label>
</div>
<div class="page">
  <div style="display:flex;justify-content:space-between;align-items:center;margin-top:16px">
    <div class="section-title" style="margin:0">Active Chats</div>
  </div>
  <div id="active-chats" class="card-grid" style="margin-top:8px"><div class="empty">Loading...</div></div>
  <div style="display:flex;justify-content:space-between;align-items:center;margin-top:16px">
    <div class="section-title" style="margin:0">Queue</div>
    <button class="btn" style="background:#7c3aed;color:#fff;font-size:12px;padding:6px 12px" onclick="showAssignModal()">+ Assign Phone</button>
  </div>
  <div id="queue-list" style="margin-top:8px"><div class="empty">Loading...</div></div>
  <div id="refresh-status"></div>

  <div id="assign-modal" style="display:none;position:fixed;inset:0;background:rgba(0,0,0,.4);z-index:100;align-items:center;justify-content:center">
    <div style="background:#fff;border-radius:12px;padding:24px;width:320px;box-shadow:0 8px 32px rgba(0,0,0,.2)">
      <div style="font-size:15px;font-weight:700;margin-bottom:16px">Assign Phone to Queue</div>
      <label style="font-size:12px;font-weight:600;display:block;margin-bottom:4px">Phone Number</label>
      <input id="assign-phone" type="text" placeholder="628123456789" style="width:100%;padding:8px 10px;border:1px solid #d1d5db;border-radius:8px;font-size:14px;margin-bottom:12px">
      <label style="font-size:12px;font-weight:600;display:block;margin-bottom:4px">Customer Name (optional)</label>
      <input id="assign-name" type="text" placeholder="Nama customer" style="width:100%;padding:8px 10px;border:1px solid #d1d5db;border-radius:8px;font-size:14px;margin-bottom:16px">
      <div style="display:flex;gap:8px">
        <button class="btn" style="background:#075E54;color:#fff;flex:1" onclick="doAssign()">Assign</button>
        <button class="btn" style="background:#e2e8f0;color:#475569;flex:1" onclick="hideAssignModal()">Cancel</button>
      </div>
    </div>
  </div>
</div>
<script>
var apiKey    = {{safeKey}};
var contactId = {{safeContactId}};
var hbInterval, refreshInterval;

function esc(s){ return String(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }

function load(){
  fetch('/api/'+apiKey+'/agent/chats', {
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body: JSON.stringify({contactId: contactId})
  })
  .then(function(r){ return r.json(); })
  .then(function(d){
    if(!d.success){
      document.getElementById('agent-name').textContent = 'Error';
      document.getElementById('agent-sub').textContent  = d.error || 'Creatio not ready';
      document.getElementById('active-chats').innerHTML = '<div class="empty" style="color:#c00">⚠ '+esc(d.error||'Creatio service error')+'</div>';
      document.getElementById('queue-list').innerHTML   = '';
      return;
    }

    document.getElementById('agent-name').textContent = d.agentName || 'Agent';
    var toggle = document.getElementById('online-toggle');
    toggle.checked = d.isOnline;
    document.getElementById('online-label').textContent = d.isOnline ? 'Online' : 'Offline';

    renderActiveChats(d.chats || []);
    renderQueue(d.queue || []);
    document.getElementById('refresh-status').textContent = 'Updated ' + new Date().toLocaleTimeString();
  })
  .catch(function(e){ console.error('Load error', e); });
}

function renderActiveChats(chats){
  var el = document.getElementById('active-chats');
  if(!chats.length){ el.innerHTML='<div class="empty">No active chats</div>'; return; }
  el.innerHTML = chats.map(function(c){
    return '<div class="chat-card">'
      +'<div class="cname">'+esc(c.customerName||c.phoneNumber)+'</div>'
      +'<div class="phone">'+esc(c.phoneNumber)+'</div>'
      +'<div class="time">Assigned: '+esc(c.assignedOn)+'</div>'
      +'<div class="actions">'
      +'<a class="btn btn-open" href="/chat/'+encodeURIComponent(apiKey)+'/'+encodeURIComponent(c.conversationId)+'?phone='+encodeURIComponent(c.phoneNumber)+'" target="_blank">Open Chat</a>'
      +'<button class="btn btn-close" onclick="closeChat(\''+esc(c.id)+'\')">Close</button>'
      +'</div>'
      +'</div>';
  }).join('');
}

function renderQueue(queue){
  var el = document.getElementById('queue-list');
  if(!queue.length){ el.innerHTML='<div class="empty">Queue empty</div>'; return; }
  el.innerHTML = queue.map(function(q){
    return '<div class="queue-card">'
      +'<div class="queue-pos">'+q.queuePosition+'</div>'
      +'<div class="queue-info"><div class="qname">'+esc(q.customerName||q.phoneNumber)+'</div><div class="qphone">'+esc(q.phoneNumber)+'</div></div>'
      +'</div>';
  }).join('');
}

function toggleOnline(isOnline){
  document.getElementById('online-label').textContent = isOnline ? 'Online' : 'Offline';
  fetch('/api/'+apiKey+'/agent/online', {
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body: JSON.stringify({contactId: contactId, isOnline: isOnline})
  }).catch(function(e){ console.error('SetOnline error', e); });
}

function closeChat(agentChatId){
  if(!confirm('Tutup chat ini?')) return;
  fetch('/api/'+apiKey+'/close', {
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body: JSON.stringify({agentChatId: agentChatId, contactId: contactId})
  })
  .then(function(r){ return r.json(); })
  .then(function(d){
    if(d.success) load();
    else alert('Gagal menutup chat: '+(d.error||'unknown'));
  })
  .catch(function(e){ alert('Error: '+e.message); });
}

function sendHeartbeat(){
  fetch('/api/'+apiKey+'/agent/heartbeat', {
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body: JSON.stringify({contactId: contactId})
  }).catch(function(e){ console.warn('Heartbeat error', e); });
}

function showAssignModal(){
  document.getElementById('assign-modal').style.display='flex';
  document.getElementById('assign-phone').focus();
}
function hideAssignModal(){
  document.getElementById('assign-modal').style.display='none';
  document.getElementById('assign-phone').value='';
  document.getElementById('assign-name').value='';
}
function doAssign(){
  var phone = document.getElementById('assign-phone').value.trim();
  var name  = document.getElementById('assign-name').value.trim();
  if(!phone){ alert('Phone number wajib diisi'); return; }
  fetch('/api/'+apiKey+'/agent/request', {
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body: JSON.stringify({phoneNumber: phone, customerName: name||phone})
  })
  .then(function(r){ return r.json(); })
  .then(function(d){
    if(d.success){ hideAssignModal(); load(); }
    else alert('Gagal: '+(d.error||'unknown'));
  })
  .catch(function(e){ alert('Error: '+e.message); });
}

// Initial load + polling
load();
refreshInterval = setInterval(load, 5000);
hbInterval      = setInterval(sendHeartbeat, 60000);

// Mark online on open
fetch('/api/'+apiKey+'/agent/online', {
  method:'POST',
  headers:{'Content-Type':'application/json'},
  body: JSON.stringify({contactId: contactId, isOnline: true})
});

// Mark offline on close
window.addEventListener('beforeunload', function(){
  fetch('/api/'+apiKey+'/agent/online', {
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body: JSON.stringify({contactId: contactId, isOnline: false}),
    keepalive: true
  });
});
</script>
</body>
</html>
""";
    }
}

public class AgentActionDto
{
    public string ContactId { get; set; } = "";
}

public class SetOnlineDto
{
    public string ContactId { get; set; } = "";
    public bool IsOnline { get; set; }
}

public class CloseChatDto
{
    public string AgentChatId { get; set; } = "";
    public string ContactId   { get; set; } = "";
}

public class RequestAgentDto
{
    public string PhoneNumber  { get; set; } = "";
    public string CustomerName { get; set; } = "";
}
