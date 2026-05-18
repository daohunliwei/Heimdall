"""Test Heimdall frontend for console errors, network failures, and API proxy issues."""
from playwright.sync_api import sync_playwright
import json

errors = []
network_failures = []
console_logs = []

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    page = browser.new_page()

    # Capture console messages
    page.on("console", lambda msg: console_logs.append({
        "type": msg.type,
        "text": msg.text
    }))

    # Capture network failures
    page.on("requestfailed", lambda req: network_failures.append({
        "url": req.url,
        "failure": req.failure
    }))

    page.on("response", lambda resp: None)  # for debugging

    print("=== Navigating to http://localhost:3000 ===")
    try:
        page.goto("http://localhost:3000", timeout=30000)
        page.wait_for_load_state("networkidle", timeout=30000)
    except Exception as e:
        print(f"PAGE LOAD ERROR: {e}")

    page.wait_for_timeout(3000)

    # Screenshot
    page.screenshot(path="scripts/frontend_screenshot.png", full_page=True)
    print("Screenshot saved to scripts/frontend_screenshot.png")

    # Check page title
    title = page.title()
    print(f"Page title: {title}")

    # Check for visible error boundary / error states
    error_elements = page.locator('[class*="error"], [class*="Error"], [data-testid*="error"]').all()
    if error_elements:
        print(f"\n=== Error elements on page ({len(error_elements)}) ===")
        for el in error_elements[:10]:
            try:
                print(f"  {el.text_content()[:200]}")
            except:
                pass

    # Check page content for common error patterns
    body_text = page.locator("body").text_content()
    if "Application error" in body_text or "Something went wrong" in body_text:
        print("\n*** REACT ERROR BOUNDARY DETECTED ***")
        print(body_text[:2000])

    print(f"\n=== Console Logs ({len(console_logs)} total) ===")
    for log in console_logs:
        marker = ""
        if log["type"] in ("error", "warning"):
            marker = " <<<"
        print(f"  [{log['type']}]{marker} {log['text'][:200]}")

    # Check console errors specifically
    console_errors = [l for l in console_logs if l["type"] == "error"]
    if console_errors:
        print(f"\n=== CONSOLE ERRORS ({len(console_errors)}) ===")
        for err in console_errors:
            print(f"  ERROR: {err['text'][:300]}")

    print(f"\n=== Network Failures ({len(network_failures)}) ===")
    for nf in network_failures[:20]:
        print(f"  FAILED: {nf['url']}")

    # Test API proxy: /api/models/config
    print("\n=== Testing API Proxy: /api/models/config ===")
    try:
        resp = page.goto("http://localhost:3000/api/models/config", timeout=15000)
        if resp:
            print(f"  Status: {resp.status}")
            if resp.status == 200:
                data = resp.json()
                print(f"  Default provider: {data.get('defaultProvider', 'N/A')}")
                print(f"  Providers: {len(data.get('providers', []))}")
            else:
                print(f"  Body: {resp.text()[:500]}")
    except Exception as e:
        print(f"  API proxy error: {e}")

    # Test API proxy: /api/auth/status
    print("\n=== Testing API Proxy: /api/auth/status ===")
    try:
        resp = page.goto("http://localhost:3000/api/auth/status", timeout=15000)
        if resp:
            print(f"  Status: {resp.status}")
            if resp.status == 200:
                print(f"  Body: {resp.text()[:200]}")
    except Exception as e:
        print(f"  API proxy error: {e}")

    # Test API proxy: /api/tasks/wiki (POST needs to go through proxy)
    print("\n=== Testing API Proxy: /api/tasks/status check ===")
    try:
        # Test through the Next.js rewrite
        resp = page.goto("http://localhost:3000/api/lang/config", timeout=15000)
        if resp:
            print(f"  /api/lang/config Status: {resp.status}")
    except Exception as e:
        print(f"  API proxy error: {e}")

    # Try to trigger a page action - type a repo URL and submit
    print("\n=== Testing Wiki Generation Form ===")
    try:
        # Look for the main input/textarea for repo URL
        inputs = page.locator("input, textarea").all()
        print(f"  Found {len(inputs)} input/textarea elements")
        for inp in inputs[:10]:
            placeholder = inp.get_attribute("placeholder") or ""
            name = inp.get_attribute("name") or ""
            print(f"  Input: name='{name}' placeholder='{placeholder[:80]}'")
    except Exception as e:
        print(f"  Error inspecting form: {e}")

    browser.close()

# Summary
print("\n========== SUMMARY ==========")
print(f"Console errors: {len(console_errors)}")
print(f"Network failures: {len(network_failures)}")
print(f"Total console logs: {len(console_logs)}")
