using ChatBridgeService.Data;
using ChatBridgeService.Models;
using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatBridgeService.Controllers;

[ApiController]
public class AdminController : ControllerBase
{
    private readonly IInstanceService _instances;
    private readonly AppDbContext _db;
    private readonly AdminSession _session;
    private readonly IConfiguration _config;

    public AdminController(IInstanceService instances, AppDbContext db, AdminSession session, IConfiguration config)
    {
        _instances = instances;
        _db = db;
        _session = session;
        _config = config;
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [HttpGet("admin/login")]
    public IActionResult LoginPage([FromQuery] string? error = null) =>
        Content(BuildLoginHtml(error), "text/html; charset=utf-8");

    [HttpPost("admin/login")]
    public IActionResult Login([FromForm] string username, [FromForm] string password)
    {
        string expectedUser = _config["Admin:Username"] ?? "admin";
        string expectedPass = _config["Admin:Password"] ?? "";

        if (username != expectedUser || password != expectedPass)
            return Redirect("/admin/login?error=1");

        string token = _session.CreateToken();
        Response.Cookies.Append("admin_token", token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });
        return Redirect("/admin");
    }

    [HttpGet("admin/logout")]
    public IActionResult Logout()
    {
        Request.Cookies.TryGetValue("admin_token", out var token);
        _session.Revoke(token);
        Response.Cookies.Delete("admin_token");
        return Redirect("/admin/login");
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("admin")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        if (!IsAuthenticated()) return Redirect("/admin/login");

        var today = DateTime.UtcNow.Date;
        var instances = await _db.CreatioInstances.ToListAsync(ct);
        var logsToday = await _db.MessageLogs.Where(l => l.CreatedAt >= today).ToListAsync(ct);

        var stats = new DashboardStats
        {
            TotalInstances = instances.Count,
            ActiveInstances = instances.Count(i => i.IsActive),
            MessagesToday = logsToday.Count(l => l.Type is "webhook_in" or "agent_reply"),
            ErrorsToday = logsToday.Count(l => !l.Success),
            InstanceStats = instances.Select(i => new InstanceStat
            {
                Instance = i,
                MessagesToday = logsToday.Count(l => l.InstanceId == i.Id && l.Type is "webhook_in" or "agent_reply"),
                ErrorsToday = logsToday.Count(l => l.InstanceId == i.Id && !l.Success),
                LastActivity = logsToday.Where(l => l.InstanceId == i.Id).OrderByDescending(l => l.CreatedAt).FirstOrDefault()?.CreatedAt
            }).ToList()
        };

        return Content(BuildDashboardHtml(stats), "text/html; charset=utf-8");
    }

    // ── Instances ─────────────────────────────────────────────────────────────

    [HttpGet("admin/instances")]
    public async Task<IActionResult> Instances(CancellationToken ct)
    {
        if (!IsAuthenticated()) return Redirect("/admin/login");
        var list = await _instances.GetAllAsync(ct);
        return Content(BuildInstancesHtml(list), "text/html; charset=utf-8");
    }

    [HttpGet("admin/instances/new")]
    public IActionResult NewForm()
    {
        if (!IsAuthenticated()) return Redirect("/admin/login");
        return Content(BuildFormHtml(null, null), "text/html; charset=utf-8");
    }

    [HttpPost("admin/instances")]
    public async Task<IActionResult> Create([FromForm] InstanceFormDto dto, CancellationToken ct)
    {
        if (!IsAuthenticated()) return Redirect("/admin/login");
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.CreatioBaseUrl))
            return Content(BuildFormHtml(null, "Name and Creatio URL are required"), "text/html; charset=utf-8");

        await _instances.CreateAsync(new CreatioInstance
        {
            Name = dto.Name.Trim(),
            ApiKey = string.IsNullOrWhiteSpace(dto.ApiKey) ? GenerateApiKey() : dto.ApiKey.Trim(),
            CreatioBaseUrl = dto.CreatioBaseUrl.TrimEnd('/'),
            CreatioIdentityUrl = dto.CreatioIdentityUrl?.TrimEnd('/') ?? "",
            CreatioClientId = dto.CreatioClientId ?? "",
            CreatioClientSecret = dto.CreatioClientSecret ?? "",
            MetaAccessToken = dto.MetaAccessToken ?? "",
            MetaPhoneNumberId = dto.MetaPhoneNumberId ?? "",
            MetaVerifyToken = dto.MetaVerifyToken ?? "",
            IsActive = dto.IsActive
        }, ct);
        return Redirect("/admin/instances");
    }

    [HttpGet("admin/instances/{id:guid}/edit")]
    public async Task<IActionResult> EditForm(Guid id, CancellationToken ct)
    {
        if (!IsAuthenticated()) return Redirect("/admin/login");
        var instance = await _instances.GetByIdAsync(id, ct);
        if (instance == null) return NotFound();
        return Content(BuildFormHtml(instance, null), "text/html; charset=utf-8");
    }

    [HttpPost("admin/instances/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromForm] InstanceFormDto dto, CancellationToken ct)
    {
        if (!IsAuthenticated()) return Redirect("/admin/login");
        var instance = await _instances.GetByIdAsync(id, ct);
        if (instance == null) return NotFound();

        instance.Name = dto.Name?.Trim() ?? instance.Name;
        instance.ApiKey = string.IsNullOrWhiteSpace(dto.ApiKey) ? instance.ApiKey : dto.ApiKey.Trim();
        instance.CreatioBaseUrl = dto.CreatioBaseUrl?.TrimEnd('/') ?? instance.CreatioBaseUrl;
        instance.CreatioIdentityUrl = dto.CreatioIdentityUrl?.TrimEnd('/') ?? instance.CreatioIdentityUrl;
        instance.CreatioClientId = dto.CreatioClientId ?? instance.CreatioClientId;
        if (!string.IsNullOrWhiteSpace(dto.CreatioClientSecret)) instance.CreatioClientSecret = dto.CreatioClientSecret;
        instance.MetaAccessToken = dto.MetaAccessToken ?? instance.MetaAccessToken;
        instance.MetaPhoneNumberId = dto.MetaPhoneNumberId ?? instance.MetaPhoneNumberId;
        instance.MetaVerifyToken = dto.MetaVerifyToken ?? instance.MetaVerifyToken;
        instance.IsActive = dto.IsActive;

        await _instances.UpdateAsync(instance, ct);
        return Redirect("/admin/instances");
    }

    [HttpPost("admin/instances/{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!IsAuthenticated()) return Redirect("/admin/login");
        await _instances.DeleteAsync(id, ct);
        return Redirect("/admin/instances");
    }

    // ── Logs ──────────────────────────────────────────────────────────────────

    [HttpGet("admin/logs")]
    public async Task<IActionResult> Logs([FromQuery] Guid? instanceId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!IsAuthenticated()) return Redirect("/admin/login");

        var instances = await _db.CreatioInstances.OrderBy(i => i.Name).ToListAsync(ct);
        int pageSize = 50;

        var query = _db.MessageLogs.AsQueryable();
        if (instanceId.HasValue) query = query.Where(l => l.InstanceId == instanceId);

        int total = await query.CountAsync(ct);
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(l => l.Instance)
            .ToListAsync(ct);

        return Content(BuildLogsHtml(logs, instances, instanceId, page, total, pageSize), "text/html; charset=utf-8");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsAuthenticated()
    {
        Request.Cookies.TryGetValue("admin_token", out var token);
        return _session.IsValid(token);
    }

    private static string GenerateApiKey() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "").Replace("/", "").Replace("=", "")[..16];

    private static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    // ── Layout ────────────────────────────────────────────────────────────────

    private static string Layout(string activePage, string title, string content)
    {
        string nav(string href, string icon, string label, string page) =>
            $"""<a href="{href}" class="nav-item{(activePage == page ? " active" : "")}">{icon}<span>{label}</span></a>""";

        return $$"""
<!DOCTYPE html>
<html lang="id">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>{{H(title)}} — ChatBridge</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f0f2f5;display:flex;height:100vh;overflow:hidden}
.sidebar{width:220px;background:#1e1e2e;color:#cdd6f4;display:flex;flex-direction:column;flex-shrink:0;height:100vh}
.sidebar-logo{padding:20px 16px 16px;font-size:18px;font-weight:700;color:#cba6f7;border-bottom:1px solid #313244;letter-spacing:.5px}
.sidebar-logo span{font-size:11px;font-weight:400;color:#6c7086;display:block;margin-top:2px}
.nav-section{padding:12px 8px 4px;font-size:10px;font-weight:700;letter-spacing:1px;color:#585b70;text-transform:uppercase;padding-left:12px}
.nav-item{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:8px;margin:2px 8px;text-decoration:none;color:#bac2de;font-size:13px;font-weight:500;transition:background .15s,color .15s}
.nav-item:hover{background:#313244;color:#cdd6f4}
.nav-item.active{background:#7c3aed;color:#fff}
.nav-item svg{width:16px;height:16px;flex-shrink:0}
.sidebar-bottom{margin-top:auto;padding:12px 8px;border-top:1px solid #313244}
.main{flex:1;display:flex;flex-direction:column;overflow:hidden}
.topbar{background:#fff;padding:14px 24px;border-bottom:1px solid #e2e8f0;display:flex;align-items:center;justify-content:space-between;flex-shrink:0}
.topbar h1{font-size:18px;font-weight:700;color:#1e1e2e}
.page-body{flex:1;overflow-y:auto;padding:24px}
.card{background:#fff;border-radius:10px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,.08);margin-bottom:16px}
.stat-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:16px;margin-bottom:20px}
.stat-card{background:#fff;border-radius:10px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,.08)}
.stat-card .val{font-size:28px;font-weight:700;color:#1e1e2e;margin-bottom:4px}
.stat-card .lbl{font-size:12px;color:#64748b;font-weight:500}
.stat-card.blue .val{color:#3b82f6}
.stat-card.green .val{color:#22c55e}
.stat-card.red .val{color:#ef4444}
.stat-card.purple .val{color:#7c3aed}
table{width:100%;border-collapse:collapse;font-size:13px}
th{text-align:left;padding:10px 12px;background:#f8fafc;color:#64748b;font-weight:600;border-bottom:1px solid #e2e8f0;font-size:12px;text-transform:uppercase;letter-spacing:.5px}
td{padding:10px 12px;border-bottom:1px solid #f1f5f9;vertical-align:middle}
tr:last-child td{border-bottom:none}
.badge{display:inline-flex;align-items:center;padding:3px 8px;border-radius:20px;font-size:11px;font-weight:600}
.badge-green{background:#dcfce7;color:#15803d}
.badge-red{background:#fee2e2;color:#dc2626}
.badge-blue{background:#dbeafe;color:#1d4ed8}
.badge-gray{background:#f1f5f9;color:#475569}
.badge-yellow{background:#fef9c3;color:#854d0e}
.btn{display:inline-flex;align-items:center;gap:6px;padding:8px 14px;border-radius:8px;font-size:13px;font-weight:600;text-decoration:none;cursor:pointer;border:none;transition:opacity .15s}
.btn:hover{opacity:.85}
.btn-primary{background:#7c3aed;color:#fff}
.btn-secondary{background:#e2e8f0;color:#475569}
.btn-danger{background:#fee2e2;color:#dc2626}
.btn-sm{padding:5px 10px;font-size:12px}
form.inline{display:inline}
label{display:block;font-size:13px;font-weight:600;margin-bottom:5px;color:#374151}
input,select{width:100%;padding:9px 12px;border:1px solid #d1d5db;border-radius:8px;font-size:14px;font-family:inherit;transition:border .15s}
input:focus,select:focus{outline:none;border-color:#7c3aed;box-shadow:0 0 0 3px rgba(124,58,237,.1)}
.input-group{margin-bottom:14px}
.form-row{display:grid;grid-template-columns:1fr 1fr;gap:16px}
.alert-error{padding:10px 14px;border-radius:8px;margin-bottom:16px;font-size:13px;background:#fee2e2;color:#dc2626;border:1px solid #fca5a5}
.mono{font-family:monospace;font-size:12px;background:#f1f5f9;padding:2px 6px;border-radius:4px}
.empty{text-align:center;color:#94a3b8;padding:40px;font-size:14px}
.pagination{display:flex;gap:4px;align-items:center;justify-content:flex-end;margin-top:16px}
.page-btn{padding:6px 12px;border-radius:6px;font-size:13px;font-weight:600;text-decoration:none;color:#475569;background:#f1f5f9}
.page-btn.active{background:#7c3aed;color:#fff}
.page-btn:hover{background:#e2e8f0}
.log-type{font-size:11px;font-weight:700;padding:2px 8px;border-radius:10px}
.log-webhook_in{background:#dbeafe;color:#1d4ed8}
.log-agent_reply{background:#dcfce7;color:#15803d}
.log-error_creatio,.log-error_meta{background:#fee2e2;color:#dc2626}
</style>
</head>
<body>
<aside class="sidebar">
  <div class="sidebar-logo">Chat Bridge <span>Admin Panel</span></div>
  <div class="nav-section">Main</div>
  {{nav("/admin", """<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/></svg>""", "Dashboard", "dashboard")}}
  <div class="nav-section">Configuration</div>
  {{nav("/admin/instances", """<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M12 1v4M12 19v4M4.22 4.22l2.83 2.83M16.95 16.95l2.83 2.83M1 12h4M19 12h4M4.22 19.78l2.83-2.83M16.95 7.05l2.83-2.83"/></svg>""", "Instances", "instances")}}
  <div class="nav-section">Monitor</div>
  {{nav("/admin/logs", """<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>""", "Logs", "logs")}}
  <div class="sidebar-bottom">
    <a href="/admin/logout" class="nav-item" style="margin:0">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
      <span>Logout</span>
    </a>
  </div>
</aside>
<div class="main">
  <div class="topbar">
    <h1>{{H(title)}}</h1>
  </div>
  <div class="page-body">
    {{content}}
  </div>
</div>
</body>
</html>
""";
    }

    // ── Login page ────────────────────────────────────────────────────────────

    private static string BuildLoginHtml(string? error)
    {
        string errorHtml = error != null ? "<div class=\"alert-error\">Invalid username or password.</div>" : "";
        return $$"""
<!DOCTYPE html>
<html lang="id">
<head>
<meta charset="utf-8">
<title>Login — ChatBridge</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f0f2f5;display:flex;align-items:center;justify-content:center;height:100vh}
.card{background:#fff;border-radius:12px;padding:40px;box-shadow:0 4px 20px rgba(0,0,0,.1);width:380px}
.logo{font-size:22px;font-weight:800;color:#7c3aed;margin-bottom:4px}
.sub{font-size:13px;color:#94a3b8;margin-bottom:28px}
label{display:block;font-size:13px;font-weight:600;margin-bottom:5px;color:#374151}
input{width:100%;padding:10px 12px;border:1px solid #d1d5db;border-radius:8px;font-size:14px;font-family:inherit;margin-bottom:14px}
input:focus{outline:none;border-color:#7c3aed;box-shadow:0 0 0 3px rgba(124,58,237,.1)}
button{width:100%;padding:11px;background:#7c3aed;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:700;cursor:pointer;transition:background .15s}
button:hover{background:#6d28d9}
.alert-error{padding:10px 12px;border-radius:8px;margin-bottom:14px;font-size:13px;background:#fee2e2;color:#dc2626}
</style>
</head>
<body>
<div class="card">
  <div class="logo">ChatBridge</div>
  <div class="sub">Admin Panel</div>
  {{errorHtml}}
  <form method="post" action="/admin/login">
    <label>Username</label>
    <input type="text" name="username" placeholder="admin" autofocus required>
    <label>Password</label>
    <input type="password" name="password" placeholder="••••••••" required>
    <button type="submit">Sign In</button>
  </form>
</div>
</body>
</html>
""";
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    private static string BuildDashboardHtml(DashboardStats s)
    {
        string instanceRows = s.InstanceStats.Count == 0
            ? "<tr><td colspan='5' class='empty'>No instances yet</td></tr>"
            : string.Join("", s.InstanceStats.Select(i =>
            {
                string status = i.Instance.IsActive
                    ? "<span class=\"badge badge-green\">Active</span>"
                    : "<span class=\"badge badge-gray\">Inactive</span>";
                string lastAct = i.LastActivity.HasValue
                    ? i.LastActivity.Value.ToLocalTime().ToString("dd/MM HH:mm")
                    : "<span style='color:#94a3b8'>-</span>";
                return $"""
                    <tr>
                      <td><strong>{H(i.Instance.Name)}</strong></td>
                      <td>{status}</td>
                      <td>{i.MessagesToday}</td>
                      <td>{(i.ErrorsToday > 0 ? $"<span class=\"badge badge-red\">{i.ErrorsToday}</span>" : "0")}</td>
                      <td>{lastAct}</td>
                    </tr>
                    """;
            }));

        string content = $"""
            <div class="stat-grid">
              <div class="stat-card purple"><div class="val">{s.TotalInstances}</div><div class="lbl">Total Instances</div></div>
              <div class="stat-card green"><div class="val">{s.ActiveInstances}</div><div class="lbl">Active Instances</div></div>
              <div class="stat-card blue"><div class="val">{s.MessagesToday}</div><div class="lbl">Messages Today</div></div>
              <div class="stat-card red"><div class="val">{s.ErrorsToday}</div><div class="lbl">Errors Today</div></div>
            </div>
            <div class="card" style="padding:0;overflow:hidden">
              <div style="padding:16px 20px;font-weight:700;font-size:14px;border-bottom:1px solid #f1f5f9">Instance Statistics</div>
              <table>
                <thead><tr><th>Instance</th><th>Status</th><th>Messages Today</th><th>Errors</th><th>Last Activity</th></tr></thead>
                <tbody>{instanceRows}</tbody>
              </table>
            </div>
            """;
        return Layout("dashboard", "Dashboard", content);
    }

    // ── Instances list ────────────────────────────────────────────────────────

    private static string BuildInstancesHtml(List<CreatioInstance> list)
    {
        string rows = list.Count == 0
            ? "<tr><td colspan='5' class='empty'>No instances yet. Click \"Add Instance\" to get started.</td></tr>"
            : string.Join("", list.Select(i =>
            {
                string status = i.IsActive
                    ? "<span class=\"badge badge-green\">Active</span>"
                    : "<span class=\"badge badge-gray\">Inactive</span>";
                return $"""
                    <tr>
                      <td><strong>{H(i.Name)}</strong></td>
                      <td><span class="mono">{H(i.ApiKey)}</span></td>
                      <td>{H(i.CreatioBaseUrl)}</td>
                      <td>{status}</td>
                      <td>
                        <a href="/admin/instances/{i.Id}/edit" class="btn btn-secondary btn-sm">Edit</a>
                        <form class="inline" method="post" action="/admin/instances/{i.Id}/delete"
                              onsubmit="return confirm('Delete instance {H(i.Name)}?')">
                          <button type="submit" class="btn btn-danger btn-sm">Delete</button>
                        </form>
                      </td>
                    </tr>
                    """;
            }));

        string content = $"""
            <div style="display:flex;justify-content:flex-end;margin-bottom:16px">
              <a href="/admin/instances/new" class="btn btn-primary">+ Add Instance</a>
            </div>
            <div class="card" style="padding:0;overflow:hidden">
              <table>
                <thead><tr><th>Name</th><th>API Key</th><th>Creatio URL</th><th>Status</th><th>Actions</th></tr></thead>
                <tbody>{rows}</tbody>
              </table>
            </div>
            """;
        return Layout("instances", "Setup Instances", content);
    }

    // ── Instance form ─────────────────────────────────────────────────────────

    private static string BuildFormHtml(CreatioInstance? inst, string? error)
    {
        bool isEdit = inst != null;
        string pageTitle = isEdit ? $"Edit: {H(inst!.Name)}" : "Add New Instance";
        string action = isEdit ? $"/admin/instances/{inst!.Id}" : "/admin/instances";
        string errorHtml = error != null ? $"<div class=\"alert-error\">{H(error)}</div>" : "";
        string selActive = inst?.IsActive != false ? "selected" : "";
        string selInactive = inst?.IsActive == false ? "selected" : "";
        string secretPlaceholder = isEdit ? "Leave blank to keep current" : "Client Secret";

        string content = $"""
            <div style="margin-bottom:16px">
              <a href="/admin/instances" class="btn btn-secondary btn-sm">← Back</a>
            </div>
            {errorHtml}
            <form method="post" action="{action}">
            <div class="card">
              <div style="font-weight:700;font-size:14px;margin-bottom:16px">Basic Information</div>
              <div class="form-row">
                <div class="input-group">
                  <label>Instance Name *</label>
                  <input name="Name" value="{H(inst?.Name)}" placeholder="Contoh: PT Maju Bersama" required>
                </div>
                <div class="input-group">
                  <label>API Key (URL routing)</label>
                  <input name="ApiKey" value="{H(inst?.ApiKey)}" placeholder="Leave blank to auto-generate">
                </div>
              </div>
              <div class="input-group">
                <label>Status</label>
                <select name="IsActive">
                  <option value="true" {selActive}>Active</option>
                  <option value="false" {selInactive}>Inactive</option>
                </select>
              </div>
            </div>
            <div class="card">
              <div style="font-weight:700;font-size:14px;margin-bottom:16px">Creatio Configuration</div>
              <div class="form-row">
                <div class="input-group">
                  <label>Creatio Base URL *</label>
                  <input name="CreatioBaseUrl" value="{H(inst?.CreatioBaseUrl)}" placeholder="http://localhost:8180" required>
                </div>
                <div class="input-group">
                  <label>Identity Server URL *</label>
                  <input name="CreatioIdentityUrl" value="{H(inst?.CreatioIdentityUrl)}" placeholder="http://localhost:8190">
                </div>
              </div>
              <div class="form-row">
                <div class="input-group">
                  <label>Client ID</label>
                  <input name="CreatioClientId" value="{H(inst?.CreatioClientId)}" placeholder="OAuth Client ID">
                </div>
                <div class="input-group">
                  <label>Client Secret</label>
                  <input type="password" name="CreatioClientSecret" placeholder="{secretPlaceholder}">
                </div>
              </div>
            </div>
            <div class="card">
              <div style="font-weight:700;font-size:14px;margin-bottom:16px">Meta WhatsApp Configuration</div>
              <div class="input-group">
                <label>Access Token</label>
                <input name="MetaAccessToken" value="{H(inst?.MetaAccessToken)}" placeholder="EAAxxxxx...">
              </div>
              <div class="form-row">
                <div class="input-group">
                  <label>Phone Number ID</label>
                  <input name="MetaPhoneNumberId" value="{H(inst?.MetaPhoneNumberId)}" placeholder="123456789">
                </div>
                <div class="input-group">
                  <label>Verify Token</label>
                  <input name="MetaVerifyToken" value="{H(inst?.MetaVerifyToken)}" placeholder="token-rahasia-webhook">
                </div>
              </div>
            </div>
            <div style="display:flex;gap:10px">
              <button type="submit" class="btn btn-primary">{(isEdit ? "Save Changes" : "Create Instance")}</button>
              <a href="/admin/instances" class="btn btn-secondary">Cancel</a>
            </div>
            </form>
            """;
        return Layout("instances", pageTitle, content);
    }

    // ── Logs ──────────────────────────────────────────────────────────────────

    private static string BuildLogsHtml(List<MessageLog> logs, List<CreatioInstance> instances,
        Guid? selectedInstanceId, int page, int total, int pageSize)
    {
        string instanceOptions = "<option value=''>All Instances</option>" +
            string.Join("", instances.Select(i =>
                $"<option value='{i.Id}'{(i.Id == selectedInstanceId ? " selected" : "")}>{H(i.Name)}</option>"));

        string rows = logs.Count == 0
            ? "<tr><td colspan='6' class='empty'>No logs found</td></tr>"
            : string.Join("", logs.Select(l =>
            {
                string successBadge = l.Success
                    ? "<span class=\"badge badge-green\">OK</span>"
                    : "<span class=\"badge badge-red\">Error</span>";
                string typeBadge = $"<span class=\"log-type log-{l.Type}\">{l.Type}</span>";

                string detail = H(l.Details);
                string detailCell;
                if (string.IsNullOrEmpty(l.Details) || l.Details.Length <= 80)
                {
                    detailCell = $"<span style='font-size:12px'>{detail}</span>";
                }
                else
                {
                    string preview = H(l.Details[..80]) + "…";
                    detailCell = $"""
                        <span class="detail-preview" style="font-size:12px">{preview}</span>
                        <button class="btn-expand" onclick="toggleDetail(this)">Show</button>
                        <div class="detail-full" style="display:none;font-size:12px;white-space:pre-wrap;margin-top:4px">{detail}</div>
                        """;
                }

                return $"""
                    <tr>
                      <td>{l.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}</td>
                      <td>{H(l.Instance?.Name)}</td>
                      <td>{typeBadge}</td>
                      <td>{H(l.PhoneNumber)}</td>
                      <td>{successBadge}</td>
                      <td style="max-width:360px">{detailCell}</td>
                    </tr>
                    """;
            }));

        int totalPages = (int)Math.Ceiling(total / (double)pageSize);
        string pagination = totalPages <= 1 ? "" : $"""
            <div class="pagination">
              <span style="font-size:12px;color:#94a3b8;margin-right:8px">{total} total</span>
              {(page > 1 ? $"<a href='/admin/logs?page={page - 1}{(selectedInstanceId.HasValue ? $"&instanceId={selectedInstanceId}" : "")}' class='page-btn'>‹</a>" : "")}
              {string.Join("", Enumerable.Range(Math.Max(1, page - 2), Math.Min(5, totalPages)).Select(p =>
                  $"<a href='/admin/logs?page={p}{(selectedInstanceId.HasValue ? $"&instanceId={selectedInstanceId}" : "")}' class='page-btn{(p == page ? " active" : "")}'>{p}</a>"))}
              {(page < totalPages ? $"<a href='/admin/logs?page={page + 1}{(selectedInstanceId.HasValue ? $"&instanceId={selectedInstanceId}" : "")}' class='page-btn'>›</a>" : "")}
            </div>
            """;

        string extraStyles = "<style>.btn-expand{background:none;border:none;color:#7c3aed;font-size:11px;font-weight:700;cursor:pointer;padding:0 4px;text-decoration:underline}.btn-expand:hover{color:#6d28d9}</style>";
        string extraScript = "<script>function toggleDetail(b){var p=b.previousElementSibling,f=b.nextElementSibling,h=f.style.display==='none';f.style.display=h?'block':'none';p.style.display=h?'none':'inline';b.textContent=h?'Hide':'Show';}</script>";

        string content = $"""
            {extraStyles}{extraScript}
            <div style="display:flex;gap:12px;align-items:center;margin-bottom:16px">
              <form method="get" action="/admin/logs" style="display:flex;gap:8px;align-items:center">
                <select name="instanceId" style="width:220px;margin-bottom:0" onchange="this.form.submit()">
                  {instanceOptions}
                </select>
              </form>
            </div>
            <div class="card" style="padding:0;overflow:hidden">
              <table>
                <thead><tr><th>Time</th><th>Instance</th><th>Type</th><th>Phone</th><th>Status</th><th>Detail</th></tr></thead>
                <tbody>{rows}</tbody>
              </table>
            </div>
            {pagination}
            """;
        return Layout("logs", "Logs", content);
    }
}

// ── DTOs & Models ─────────────────────────────────────────────────────────────

public class InstanceFormDto
{
    public string? Name { get; set; }
    public string? ApiKey { get; set; }
    public string? CreatioBaseUrl { get; set; }
    public string? CreatioIdentityUrl { get; set; }
    public string? CreatioClientId { get; set; }
    public string? CreatioClientSecret { get; set; }
    public string? MetaAccessToken { get; set; }
    public string? MetaPhoneNumberId { get; set; }
    public string? MetaVerifyToken { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DashboardStats
{
    public int TotalInstances { get; set; }
    public int ActiveInstances { get; set; }
    public int MessagesToday { get; set; }
    public int ErrorsToday { get; set; }
    public List<InstanceStat> InstanceStats { get; set; } = [];
}

public class InstanceStat
{
    public required CreatioInstance Instance { get; set; }
    public int MessagesToday { get; set; }
    public int ErrorsToday { get; set; }
    public DateTime? LastActivity { get; set; }
}
