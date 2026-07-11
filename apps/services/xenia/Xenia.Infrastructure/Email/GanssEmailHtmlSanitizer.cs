using Ganss.Xss;
using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// HtmlSanitizer (Ganss.Xss) backed email HTML sanitizer.
///
/// Removes or neutralizes dangerous constructs from email HTML bodies:
/// - Script elements and inline event handlers (onclick, onload, onerror, etc.)
/// - iframe, frame, form, input, button, object, embed, base, meta
/// - SVG with active scripting
/// - javascript: and vbscript: URL schemes
/// - data: URLs (except safe image data: URLs when images are allowed)
/// - Remote images and external resource loads (configurable — default: blocked)
/// - Tracking pixels and external stylesheet loads
/// - meta-refresh directives
///
/// Storage policy: raw HTML is stored in body_html as received from the provider.
///                 Sanitized HTML is returned from this service for display only.
///                 The sanitizer is also applied at normalization time so the stored
///                 value is pre-sanitized before persistence.
/// </summary>
internal sealed class GanssEmailHtmlSanitizer : IEmailHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public bool BlocksRemoteImages { get; }

    public GanssEmailHtmlSanitizer(IOptions<XeniaIngestionOptions> opts)
    {
        BlocksRemoteImages = opts.Value.BlockRemoteImages;

        _sanitizer = new HtmlSanitizer();

        // ── Remove dangerous/form-related tags ────────────────────────────────
        _sanitizer.AllowedTags.Remove("script");
        _sanitizer.AllowedTags.Remove("noscript");
        _sanitizer.AllowedTags.Remove("iframe");
        _sanitizer.AllowedTags.Remove("frame");
        _sanitizer.AllowedTags.Remove("frameset");
        _sanitizer.AllowedTags.Remove("form");
        _sanitizer.AllowedTags.Remove("input");
        _sanitizer.AllowedTags.Remove("textarea");
        _sanitizer.AllowedTags.Remove("select");
        _sanitizer.AllowedTags.Remove("option");
        _sanitizer.AllowedTags.Remove("button");
        _sanitizer.AllowedTags.Remove("object");
        _sanitizer.AllowedTags.Remove("embed");
        _sanitizer.AllowedTags.Remove("applet");
        _sanitizer.AllowedTags.Remove("base");
        _sanitizer.AllowedTags.Remove("meta");
        _sanitizer.AllowedTags.Remove("svg");
        _sanitizer.AllowedTags.Remove("math");
        _sanitizer.AllowedTags.Remove("link");

        // ── Remove event-handler attributes ──────────────────────────────────
        foreach (var eventAttr in EventHandlerAttributes())
            _sanitizer.AllowedAttributes.Remove(eventAttr);

        // ── Remove dangerous URL schemes ──────────────────────────────────────
        _sanitizer.AllowedSchemes.Remove("javascript");
        _sanitizer.AllowedSchemes.Remove("vbscript");
        _sanitizer.AllowedSchemes.Remove("data");

        // ── Remote image / resource blocking ──────────────────────────────────
        if (BlocksRemoteImages)
        {
            _sanitizer.FilterUrl += (_, e) =>
            {
                var url = e.OriginalUrl ?? string.Empty;
                var isRemote = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                            || url.StartsWith("//", StringComparison.OrdinalIgnoreCase);

                if (isRemote && IsImageContext(e.Tag?.TagName))
                    e.SanitizedUrl = null;
            };
        }
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        try
        {
            return _sanitizer.Sanitize(html) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsImageContext(string? tag) =>
        tag is not null &&
        (tag.Equals("img", StringComparison.OrdinalIgnoreCase)
         || tag.Equals("image", StringComparison.OrdinalIgnoreCase)
         || tag.Equals("source", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> EventHandlerAttributes() =>
    [
        "onclick", "ondblclick", "onmousedown", "onmouseup", "onmouseover",
        "onmousemove", "onmouseout", "onmouseenter", "onmouseleave",
        "onkeydown", "onkeypress", "onkeyup",
        "onfocus", "onblur", "onchange", "oninput", "onsubmit", "onreset",
        "onload", "onerror", "onabort", "onbeforeunload", "onunload",
        "onscroll", "onresize", "oncontextmenu",
        "ondragstart", "ondrag", "ondragenter", "ondragleave", "ondragover",
        "ondrop", "ondragend",
        "onmessage", "onstorage", "onoffline", "ononline", "onhashchange",
        "onpopstate", "onpagehide", "onpageshow",
        "ontouchstart", "ontouchmove", "ontouchend", "ontouchcancel",
        "onpointerdown", "onpointerup", "onpointermove", "onpointerover",
        "onpointerout", "onpointerenter", "onpointerleave", "onpointercancel",
        "onanimationstart", "onanimationend", "onanimationiteration",
        "ontransitionend", "ontransitionstart",
        "onwheel", "oncut", "oncopy", "onpaste",
    ];
}
