using Business.DomainEvents;
using System.Text;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Manages Black Screen protection - DOM manipulation to hide sensitive info from remote viewer
/// ASPS-369: Black Screen Implementation
/// </summary>
public class BlackScreenManager
{
    private readonly List<string> _defaultSensitiveSelectors;

    public BlackScreenManager()
    {
        // Default CSS selectors for sensitive elements
        _defaultSensitiveSelectors = new List<string>
        {
            // Banking selectors
            ".account-balance",
            ".balance",
            ".card-number",
            ".credit-card",
            "[data-sensitive='true']",
            ".cvv",
            ".pin-code",
            
            // Generic sensitive fields
            "input[type='password']",
            "input[name*='cvv']",
            "input[name*='pin']",
            "input[name*='card']",
            
            // Account numbers
            ".account-number",
            ".iban",
            ".routing-number"
        };
    }

    /// <summary>
    /// Activate black screen protection for a device
    /// </summary>
    /// <param name="userKeyField">User key</param>
    /// <param name="deviceUid">Device UID</param>
    /// <param name="targetUrl">Target URL being protected</param>
    /// <param name="remoteAccessApp">Remote access app name</param>
    /// <param name="reason">Reason for activation</param>
    /// <param name="customSelectors">Additional custom CSS selectors to hide</param>
    /// <returns>BlackScreenActivated event</returns>
    public BlackScreenActivated ActivateBlackScreen(
        string userKeyField,
        string deviceUid,
        string targetUrl,
        string remoteAccessApp,
        string reason,
        List<string>? customSelectors = null)
    {
        var selectors = new List<string>(_defaultSensitiveSelectors);
        if (customSelectors != null)
            selectors.AddRange(customSelectors);

        var script = GenerateHidingScript(selectors);

        return new BlackScreenActivated(
            userKeyField: userKeyField,
            deviceUid: deviceUid,
            targetUrl: targetUrl,
            remoteAccessApp: remoteAccessApp,
            hiddenElements: selectors,
            injectedScript: script,
            reason: reason
        );
    }

    /// <summary>
    /// Generate JavaScript/CSS to hide sensitive elements from remote viewer
    /// Uses DOM manipulation + CSS injection
    /// Local user sees everything normally, remote viewer sees black boxes
    /// </summary>
    /// <param name="selectors">CSS selectors to hide</param>
    /// <returns>JavaScript code to inject</returns>
    public string GenerateHidingScript(List<string> selectors)
    {
        var sb = new StringBuilder();

        sb.AppendLine("(function() {");
        sb.AppendLine("  // ASPS Black Screen Protection");
        sb.AppendLine("  const ASPS_HIDE_CLASS = 'asps-black-screen-hidden';");
        sb.AppendLine();
        
        // Create CSS style
        sb.AppendLine("  const style = document.createElement('style');");
        sb.AppendLine("  style.textContent = `");
        sb.AppendLine("    .${ASPS_HIDE_CLASS} {");
        sb.AppendLine("      background: black !important;");
        sb.AppendLine("      color: transparent !important;");
        sb.AppendLine("      border: 2px solid black !important;");
        sb.AppendLine("      text-shadow: none !important;");
        sb.AppendLine("      -webkit-text-fill-color: transparent !important;");
        sb.AppendLine("    }");
        sb.AppendLine("    .${ASPS_HIDE_CLASS}::placeholder {");
        sb.AppendLine("      color: transparent !important;");
        sb.AppendLine("    }");
        sb.AppendLine("  `;");
        sb.AppendLine("  document.head.appendChild(style);");
        sb.AppendLine();

        // Apply to all matching selectors
        sb.AppendLine("  const selectors = [");
        foreach (var selector in selectors)
        {
            sb.AppendLine($"    '{selector.Replace("'", "\\'")}',");
        }
        sb.AppendLine("  ];");
        sb.AppendLine();

        sb.AppendLine("  selectors.forEach(selector => {");
        sb.AppendLine("    try {");
        sb.AppendLine("      const elements = document.querySelectorAll(selector);");
        sb.AppendLine("      elements.forEach(el => {");
        sb.AppendLine("        el.classList.add(ASPS_HIDE_CLASS);");
        sb.AppendLine("        // Also hide parent containers for better coverage");
        sb.AppendLine("        if (el.parentElement) {");
        sb.AppendLine("          el.parentElement.classList.add(ASPS_HIDE_CLASS);");
        sb.AppendLine("        }");
        sb.AppendLine("      });");
        sb.AppendLine("    } catch (e) {");
        sb.AppendLine("      console.warn('ASPS: Failed to hide elements for selector', selector, e);");
        sb.AppendLine("    }");
        sb.AppendLine("  });");
        sb.AppendLine();

        // Mutation observer to handle dynamically added elements
        sb.AppendLine("  const observer = new MutationObserver(mutations => {");
        sb.AppendLine("    selectors.forEach(selector => {");
        sb.AppendLine("      try {");
        sb.AppendLine("        const elements = document.querySelectorAll(selector);");
        sb.AppendLine("        elements.forEach(el => {");
        sb.AppendLine("          if (!el.classList.contains(ASPS_HIDE_CLASS)) {");
        sb.AppendLine("            el.classList.add(ASPS_HIDE_CLASS);");
        sb.AppendLine("          }");
        sb.AppendLine("        });");
        sb.AppendLine("      } catch (e) {}");
        sb.AppendLine("    });");
        sb.AppendLine("  });");
        sb.AppendLine();

        sb.AppendLine("  observer.observe(document.body, {");
        sb.AppendLine("    childList: true,");
        sb.AppendLine("    subtree: true");
        sb.AppendLine("  });");
        sb.AppendLine();

        sb.AppendLine("  console.log('ASPS Black Screen Protection: Activated');");
        sb.AppendLine("})();");

        return sb.ToString();
    }

    /// <summary>
    /// Get default sensitive element selectors
    /// </summary>
    public List<string> GetDefaultSelectors() => new List<string>(_defaultSensitiveSelectors);

    /// <summary>
    /// Add custom selector to defaults
    /// </summary>
    public void AddDefaultSelector(string selector)
    {
        if (!_defaultSensitiveSelectors.Contains(selector))
            _defaultSensitiveSelectors.Add(selector);
    }

    /// <summary>
    /// Remove selector from defaults
    /// </summary>
    public void RemoveDefaultSelector(string selector)
    {
        _defaultSensitiveSelectors.Remove(selector);
    }
}
