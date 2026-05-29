using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ChatBridgeService.Controllers;

[ApiController]
public class ChatController : ControllerBase
{
    private readonly IInstanceService _instances;
    private readonly ICreatioForwarder _creatio;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IInstanceService instances, ICreatioForwarder creatio, ILogger<ChatController> logger)
    {
        _instances = instances;
        _creatio = creatio;
        _logger = logger;
    }

    [HttpHead("chat/{apiKey}/{conversationId}")]
    public IActionResult HeadChat(string apiKey, string conversationId) => Ok();

    [HttpGet("chat/{apiKey}/{conversationId}")]
    public async Task<IActionResult> ChatPage(string apiKey, string conversationId, [FromQuery] string phone = "", CancellationToken ct = default)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return NotFound("Instance not found");
        return Content(BuildChatHtml(apiKey, conversationId, phone), "text/html; charset=utf-8");
    }

    [HttpGet("api/{apiKey}/messages/{conversationId}")]
    public async Task<IActionResult> GetMessages(string apiKey, string conversationId, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return Ok(new { success = false, error = "Instance not found" });

        try
        {
            var json = await _creatio.GetMessagesAsync(instance, conversationId, ct);
            try
            {
                JsonDocument.Parse(json);
                return Content(json, "application/json");
            }
            catch
            {
                _logger.LogError("GetMessages: Creatio return non-JSON for instance {Name}: {Body}", instance.Name, json[..Math.Min(200, json.Length)]);
                return Ok(new { success = false, error = "Creatio service not ready. Make sure ChatBridgeAgentService is compiled in Creatio." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMessages error for instance {Name}", instance.Name);
            return Ok(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("api/{apiKey}/reply")]
    public async Task<IActionResult> Reply(string apiKey, [FromBody] AgentReplyDto body, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return Ok(new { success = false, error = "Instance not found" });

        try
        {
            if (string.IsNullOrWhiteSpace(body.PhoneNumber) || string.IsNullOrWhiteSpace(body.Message))
                return Ok(new { success = false, error = "phoneNumber and message are required" });

            var json = await _creatio.AgentReplyAsync(instance, body.PhoneNumber, body.Message, ct);
            try
            {
                JsonDocument.Parse(json);
                return Content(json, "application/json");
            }
            catch
            {
                _logger.LogError("AgentReply: Creatio return non-JSON for instance {Name}: {Body}", instance.Name, json[..Math.Min(300, json.Length)]);
                return Ok(new { success = false, error = $"Creatio error: {json[..Math.Min(100, json.Length)]}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AgentReply error for instance {Name}", instance.Name);
            return Ok(new { success = false, error = ex.Message });
        }
    }

    private static string BuildChatHtml(string apiKey, string conversationId, string phoneNumber)
    {
        var safeKey   = JsonSerializer.Serialize(apiKey);
        var safeId    = JsonSerializer.Serialize(conversationId);
        var safePhone = JsonSerializer.Serialize(phoneNumber);

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>ChatBridge</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;
  background:#ECE5DD;height:100vh;display:flex;flex-direction:column;overflow:hidden}
#header{background:#075E54;color:#fff;padding:10px 16px;font-size:14px;font-weight:600;
  display:flex;align-items:center;gap:10px;flex-shrink:0}
#header .avatar{width:36px;height:36px;border-radius:50%;background:#25D366;
  display:flex;align-items:center;justify-content:center;font-size:16px}
#msgs{flex:1;overflow-y:auto;padding:12px 16px;display:flex;flex-direction:column;gap:4px}
.row{display:flex;width:100%}
.row.L{justify-content:flex-start}
.row.R{justify-content:flex-end}
.bubble{max-width:72%;padding:7px 12px 4px;font-size:13.5px;line-height:1.5;
  word-break:break-word;box-shadow:0 1px 2px rgba(0,0,0,.13)}
.bubble.C{background:#fff;border-radius:0 10px 10px 10px}
.bubble.B,.bubble.A{background:#D9FDD3;border-radius:10px 0 10px 10px}
.lbl{font-size:11.5px;font-weight:700;margin-bottom:2px}
.bubble.C .lbl{color:#075E54}
.bubble.B .lbl,.bubble.A .lbl{color:#128C7E}
.t{font-size:11px;color:#aaa;text-align:right;margin-top:3px}
.empty{text-align:center;color:#aaa;padding:40px;font-size:13px}
#bar{background:#f0f2f5;padding:10px 12px;display:flex;gap:8px;
  align-items:flex-end;border-top:1px solid #ddd;flex-shrink:0}
#inp{flex:1;border:none;border-radius:20px;padding:9px 14px;font-size:14px;
  resize:none;outline:none;max-height:120px;background:#fff;font-family:inherit}
#btn{background:#128C7E;color:#fff;border:none;border-radius:50%;width:44px;height:44px;
  cursor:pointer;font-size:20px;display:flex;align-items:center;justify-content:center;
  flex-shrink:0;transition:background .15s}
#btn:hover{background:#0e7268}
#btn:disabled{background:#aaa;cursor:default}
#status{font-size:11px;color:#888;text-align:center;padding:3px 0;flex-shrink:0}
</style>
</head>
<body>
<div id="header">
  <div class="avatar">💬</div>
  <div><div id="hdr-phone" style="font-size:13px;opacity:.85"></div></div>
</div>
<div id="msgs"><div class="empty">Loading messages...</div></div>
<div id="status"></div>
<div id="bar">
  <textarea id="inp" rows="1" placeholder="Type a message..." onkeydown="onKey(event)"></textarea>
  <button id="btn" onclick="send()">&#10148;</button>
</div>
<script>
var apiKey = {{safeKey}};
var convId = {{safeId}};
var phone  = {{safePhone}};
document.getElementById('hdr-phone').textContent = phone || convId;

function esc(s){
  return String(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

function render(msgs){
  var el = document.getElementById('msgs');
  if(!msgs||!msgs.length){el.innerHTML='<div class="empty">No messages yet</div>';return;}
  var h='';
  msgs.forEach(function(m){
    var raw=m.message||'', time=m.createdOn||'';
    var type='C';
    if(raw.indexOf('[Bot]')===0)   type='B';
    if(raw.indexOf('[Agent]')===0) type='A';
    var text=raw.replace(/^\[(Customer|Bot|Agent)\]\s*/,'');
    var lbl=type==='C'?'Customer':(type==='A'?'Agent':'Bot');
    var side=(type==='B'||type==='A')?'R':'L';
    h+='<div class="row '+side+'"><div class="bubble '+type+'">';
    h+='<div class="lbl">'+lbl+'</div>';
    h+='<div>'+esc(text)+'</div>';
    h+='<div class="t">'+esc(time)+'</div>';
    h+='</div></div>';
  });
  el.innerHTML=h;
  el.scrollTop=el.scrollHeight;
}

function showError(msg){
  document.getElementById('msgs').innerHTML=
    '<div class="empty" style="color:#c00">&#9888; '+esc(msg)+'</div>';
}

function load(){
  fetch('/api/'+apiKey+'/messages/'+encodeURIComponent(convId))
    .then(function(r){return r.json();})
    .then(function(d){
      if(d.success){
        render(d.messages);
        document.getElementById('status').textContent=
          'Updated at '+new Date().toLocaleTimeString();
      } else {
        showError(d.error||'Failed to load messages');
      }
    })
    .catch(function(e){ showError('Cannot connect to server: '+e.message); });
}

function onKey(e){
  if(e.key==='Enter'&&!e.shiftKey){e.preventDefault();send();}
}

function send(){
  var inp=document.getElementById('inp');
  var text=inp.value.trim();
  if(!text||!phone) return;
  var btn=document.getElementById('btn');
  inp.disabled=btn.disabled=true;
  fetch('/api/'+apiKey+'/reply',{
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body:JSON.stringify({phoneNumber:phone,message:text})
  })
  .then(function(r){return r.json();})
  .then(function(d){
    if(d.success){inp.value='';load();}
    else alert('Failed: '+(d.error||'unknown'));
  })
  .catch(function(e){alert('Error: '+e.message);})
  .finally(function(){inp.disabled=btn.disabled=false;inp.focus();});
}

load();
setInterval(load, 5000);
</script>
</body>
</html>
""";
    }
}

public class AgentReplyDto
{
    public string PhoneNumber { get; set; } = "";
    public string Message { get; set; } = "";
}
