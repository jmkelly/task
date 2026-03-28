document.addEventListener('DOMContentLoaded', () => {
    const root = document.documentElement;
    const themeToggle = document.getElementById('theme-toggle');
    const themeIcon = document.getElementById('theme-toggle-icon');
    const footerYear = document.getElementById('footer-year');
    const storedTheme = localStorage.getItem('task-marketing-theme');
    const installTabGroups = document.querySelectorAll('[data-install-tabs]');
    const sourceVersionNodes = document.querySelectorAll('[data-source-version]');

    function getEffectiveTheme() {
        const theme = root.dataset.theme;

        if (theme === 'light' || theme === 'dark') {
            return theme;
        }

        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    function updateThemeToggleLabel() {
        if (!themeToggle || !themeIcon) {
            return;
        }

        const effectiveTheme = getEffectiveTheme();
        const nextTheme = effectiveTheme === 'dark' ? 'light' : 'dark';

        themeIcon.textContent = effectiveTheme === 'dark' ? '☀️' : '🌙';
        themeToggle.setAttribute('aria-label', `Switch to ${nextTheme} theme`);
        themeToggle.setAttribute('title', `Switch to ${nextTheme} theme`);
    }

    function setTheme(theme) {
        root.dataset.theme = theme;

        if (theme === 'light' || theme === 'dark') {
            localStorage.setItem('task-marketing-theme', theme);
        } else {
            localStorage.removeItem('task-marketing-theme');
        }

        updateThemeToggleLabel();
    }

    function activateInstallTab(group, nextTab, moveFocus) {
        const tabs = Array.from(group.querySelectorAll('[data-install-tab]'));
        const panels = Array.from(group.querySelectorAll('[data-install-panel]'));
        const selectedPanelId = nextTab.getAttribute('aria-controls');

        tabs.forEach((tab) => {
            const isSelected = tab === nextTab;
            tab.setAttribute('aria-selected', String(isSelected));
            tab.tabIndex = isSelected ? 0 : -1;
        });

        panels.forEach((panel) => {
            panel.hidden = panel.id !== selectedPanelId;
        });

        if (moveFocus) {
            nextTab.focus();
        }
    }

    function handleInstallTabKeydown(group, event) {
        const tabs = Array.from(group.querySelectorAll('[data-install-tab]'));
        const currentIndex = tabs.indexOf(event.currentTarget);

        if (currentIndex === -1) {
            return;
        }

        let nextIndex = currentIndex;

        switch (event.key) {
            case 'ArrowRight':
            case 'ArrowDown':
                nextIndex = (currentIndex + 1) % tabs.length;
                break;
            case 'ArrowLeft':
            case 'ArrowUp':
                nextIndex = (currentIndex - 1 + tabs.length) % tabs.length;
                break;
            case 'Home':
                nextIndex = 0;
                break;
            case 'End':
                nextIndex = tabs.length - 1;
                break;
            default:
                return;
        }

        event.preventDefault();
        activateInstallTab(group, tabs[nextIndex], true);
    }

    async function loadSourceVersion() {
        try {
            const csprojResponse = await fetch('Task.Cli/Task.Cli.csproj', { cache: 'no-store' });

            if (csprojResponse.ok) {
                const csprojText = await csprojResponse.text();
                const versionMatch = csprojText.match(/<Version>([^<]+)<\/Version>/i);

                if (versionMatch && versionMatch[1]) {
                    return versionMatch[1].trim();
                }
            }
        } catch (error) {
            console.error('Failed to load version from csproj', error);
        }

        try {
            const manifestResponse = await fetch('installers/installer-manifest.json', { cache: 'no-store' });

            if (!manifestResponse.ok) {
                throw new Error(`Unexpected response status ${manifestResponse.status}`);
            }

            const manifest = await manifestResponse.json();
            return typeof manifest.sourceVersion === 'string' ? manifest.sourceVersion.trim() : '';
        } catch (error) {
            console.error('Failed to load installer metadata fallback', error);
            return '';
        }
    }

    async function hydrateInstallerMetadata() {
        if (!sourceVersionNodes.length) {
            return;
        }

        try {
            const version = await loadSourceVersion();

            if (!version) {
                throw new Error('Unable to resolve source version metadata.');
            }

            sourceVersionNodes.forEach((node) => {
                node.textContent = `Source version ${version}`;
            });
        } catch (error) {
            console.error('Failed to load installer metadata', error);
        }
    }

    function initCopyButtons() {
        document.querySelectorAll('.copy-btn[data-copy-text]').forEach((btn) => {
            btn.addEventListener('click', async () => {
                const text = btn.getAttribute('data-copy-text');
                const original = btn.textContent;

                try {
                    if (navigator.clipboard && window.isSecureContext) {
                        await navigator.clipboard.writeText(text);
                    } else {
                        const ta = document.createElement('textarea');
                        ta.value = text;
                        ta.style.position = 'absolute';
                        ta.style.opacity = '0';
                        document.body.appendChild(ta);
                        ta.select();
                        document.execCommand('copy');
                        document.body.removeChild(ta);
                    }

                    btn.textContent = 'Copied!';
                    setTimeout(() => {
                        btn.textContent = original;
                    }, 1500);
                } catch (err) {
                    btn.textContent = 'Failed';
                    setTimeout(() => {
                        btn.textContent = original;
                    }, 1500);
                    console.error('Failed to copy text', err);
                }
            });
        });
    }

    setTheme(storedTheme === 'light' || storedTheme === 'dark' ? storedTheme : 'auto');

    if (themeToggle) {
        themeToggle.addEventListener('click', () => {
            setTheme(getEffectiveTheme() === 'dark' ? 'light' : 'dark');
        });
    }

    const colorSchemeQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handleColorSchemeChange = () => {
        if (root.dataset.theme !== 'light' && root.dataset.theme !== 'dark') {
            updateThemeToggleLabel();
        }
    };

    if (typeof colorSchemeQuery.addEventListener === 'function') {
        colorSchemeQuery.addEventListener('change', handleColorSchemeChange);
    } else if (typeof colorSchemeQuery.addListener === 'function') {
        colorSchemeQuery.addListener(handleColorSchemeChange);
    }

    if (footerYear) {
        footerYear.textContent = String(new Date().getFullYear());
    }

    installTabGroups.forEach((group) => {
        const tabs = Array.from(group.querySelectorAll('[data-install-tab]'));
        const selectedTab = tabs.find((tab) => tab.getAttribute('aria-selected') === 'true') || tabs[0];

        if (!selectedTab) {
            return;
        }

        activateInstallTab(group, selectedTab, false);

        tabs.forEach((tab) => {
            tab.addEventListener('click', () => {
                activateInstallTab(group, tab, false);
            });

            tab.addEventListener('keydown', (event) => {
                handleInstallTabKeydown(group, event);
            });
        });
    });

    hydrateInstallerMetadata();
    initCopyButtons();
});
