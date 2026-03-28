const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.setViewportSize({ width: 1200, height: 900 });
  await page.goto('http://127.0.0.1:33800/');
  await page.screenshot({ path: 'docs/assets/images/board.png', animations: 'disabled' });
  
  await page.click('button:has-text("Add Task")');
  await page.waitForTimeout(500); // wait for modal
  await page.screenshot({ path: 'docs/assets/images/add-task.png', animations: 'disabled' });
  
  await browser.close();
})();
