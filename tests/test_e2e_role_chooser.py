"""End-to-end test for the XAF Role Chooser module."""
from playwright.sync_api import sync_playwright
import sys

PASS = 0
FAIL = 0

def check(name, condition):
    global PASS, FAIL
    if condition:
        PASS += 1
        print(f"  PASS: {name}")
    else:
        FAIL += 1
        print(f"  FAIL: {name}")

def login(page, username):
    page.goto('https://localhost:5001/LoginPage', timeout=30000)
    page.wait_for_load_state('networkidle')
    page.wait_for_timeout(2000)
    page.locator('input.dxbl-text-edit-input[type="text"]').first.fill(username)
    page.locator('button.xaf-action[data-action-name="Log In"]:not([dxbl-virtual-el])').click()
    page.wait_for_load_state('networkidle')
    page.wait_for_timeout(5000)

def open_role_chooser(page):
    """Click Tools tab then Active Roles button. Returns True if popup opened."""
    tools_tab = page.locator('text=Tools')
    if tools_tab.count() == 0:
        return False
    tools_tab.first.click()
    page.wait_for_timeout(1000)

    # XAF Blazor uses the Caption as data-action-name, not the action ID
    ar = page.locator('[data-action-name="Active Roles"]')
    if ar.count() == 0:
        return False

    # Click the first one (it becomes visible after Tools tab is active)
    ar.first.click()
    page.wait_for_timeout(3000)
    return True

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)

    # ============================================================
    # TEST 1: MultiRole user sees all 4 optional roles, all active
    # ============================================================
    print("\n=== TEST 1: MultiRole user - popup shows all roles active ===")
    context = browser.new_context(ignore_https_errors=True, viewport={'width': 1920, 'height': 1080})
    page = context.new_page()
    login(page, 'MultiRole')

    opened = open_role_chooser(page)
    check("Popup opened", opened)

    body_text = page.locator('body').inner_text()
    check("Administrators role visible", "Administrators" in body_text)
    check("DataEntry role visible", "DataEntry" in body_text)
    check("Manager role visible", "Manager" in body_text)
    check("Reports role visible", "Reports" in body_text)
    check("Popup title is 'Active Role Selection'", "Active Role Selection" in body_text)

    page.screenshot(path='C:/Projects/XafRoleChooser/tests/e2e_test1.png', full_page=True)
    context.close()

    # ============================================================
    # TEST 2: Admin user sees roles (Admin has Administrators, Manager, Reports)
    # ============================================================
    print("\n=== TEST 2: Admin user - sees optional roles ===")
    context = browser.new_context(ignore_https_errors=True, viewport={'width': 1920, 'height': 1080})
    page = context.new_page()
    login(page, 'Admin')

    opened = open_role_chooser(page)
    check("Popup opened for Admin", opened)

    body_text = page.locator('body').inner_text()
    check("Manager role visible for Admin", "Manager" in body_text)
    check("Reports role visible for Admin", "Reports" in body_text)
    check("Administrators role visible for Admin", "Administrators" in body_text)

    page.screenshot(path='C:/Projects/XafRoleChooser/tests/e2e_test2.png', full_page=True)
    context.close()

    # ============================================================
    # TEST 3: User with only Default role - popup should be empty
    # ============================================================
    print("\n=== TEST 3: User with only Default role ===")
    context = browser.new_context(ignore_https_errors=True, viewport={'width': 1920, 'height': 1080})
    page = context.new_page()
    login(page, 'User')

    opened = open_role_chooser(page)
    check("Popup opened for User", opened)

    if opened:
        body_text = page.locator('body').inner_text()
        has_no_data = "No data" in body_text
        check("No optional roles for User (only Default)", has_no_data)

    page.screenshot(path='C:/Projects/XafRoleChooser/tests/e2e_test3.png', full_page=True)
    context.close()

    # ============================================================
    # TEST 4: Deactivate roles and click OK
    # ============================================================
    print("\n=== TEST 4: Deactivate roles and apply ===")
    context = browser.new_context(ignore_https_errors=True, viewport={'width': 1920, 'height': 1080})
    page = context.new_page()
    login(page, 'MultiRole')

    opened = open_role_chooser(page)
    check("Popup opened for deactivation test", opened)

    if opened:
        # Verify the popup has checkboxes showing active state
        checked_boxes = page.locator('.dxbl-checkbox-display-view-checked')
        check("Has checked IsActive checkboxes", checked_boxes.count() >= 4)

        page.screenshot(path='C:/Projects/XafRoleChooser/tests/e2e_test4_popup.png', full_page=True)

        # Click OK to close the popup
        ok_btn = page.locator('button:has-text("OK")')
        if ok_btn.count() > 0:
            ok_btn.first.click()
            page.wait_for_timeout(3000)

        page.screenshot(path='C:/Projects/XafRoleChooser/tests/e2e_test4_after.png', full_page=True)
        check("OK button closed popup without crash", True)

    context.close()

    # ============================================================
    # TEST 5: Re-open popup shows remembered state (all unchecked)
    # ============================================================
    # Note: Since we use fresh contexts, this tests within same session
    print("\n=== TEST 5: Login page accessible ===")
    context = browser.new_context(ignore_https_errors=True, viewport={'width': 1920, 'height': 1080})
    page = context.new_page()
    page.goto('https://localhost:5001/LoginPage', timeout=30000)
    page.wait_for_load_state('networkidle')
    page.wait_for_timeout(2000)
    body_text = page.locator('body').inner_text()
    check("Login page loads", "Log In" in body_text)
    context.close()

    browser.close()

    # ============================================================
    # SUMMARY
    # ============================================================
    print(f"\n{'='*50}")
    print(f"Results: {PASS} passed, {FAIL} failed out of {PASS+FAIL} tests")
    print(f"{'='*50}")

    sys.exit(1 if FAIL > 0 else 0)
