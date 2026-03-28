document.addEventListener('DOMContentLoaded', () => {
    const root = document.documentElement;
    const themeToggle = document.getElementById('theme-toggle');
    const themeIcon = document.getElementById('theme-toggle-icon');
    const footerYear = document.getElementById('footer-year');
    const storedTheme = localStorage.getItem('task-marketing-theme');
    const installTabGroups = document.querySelectorAll('[data-install-tabs]');
    const sourceVersionNodes = document.querySelectorAll('[data-source-version]');

    function getEffectiveTheme() {
        return root.dataset.theme === 'light' ? 'light' : 'dark';
    }

    function updateThemeToggleLabel() {
        if (!themeToggle || !themeIcon) {
            return;
        }

        const effectiveTheme = getEffectiveTheme();
        const nextTheme = effectiveTheme === 'dark' ? 'light' : 'dark';

        themeIcon.textContent = effectiveTheme === 'dark' ? '☀' : '☾';
        themeToggle.setAttribute('aria-label', `Switch to ${nextTheme} theme`);
        themeToggle.setAttribute('title', `Switch to ${nextTheme} theme`);
    }

    function setTheme(theme) {
        root.dataset.theme = theme === 'light' ? 'light' : 'dark';
        localStorage.setItem('task-marketing-theme', root.dataset.theme);
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
        document.querySelectorAll('.copy-btn[data-copy-text]').forEach((button) => {
            button.addEventListener('click', async () => {
                const text = button.getAttribute('data-copy-text');
                const original = button.textContent;

                try {
                    if (navigator.clipboard && window.isSecureContext) {
                        await navigator.clipboard.writeText(text);
                    } else {
                        const input = document.createElement('textarea');
                        input.value = text;
                        input.style.position = 'absolute';
                        input.style.opacity = '0';
                        document.body.appendChild(input);
                        input.select();
                        document.execCommand('copy');
                        document.body.removeChild(input);
                    }

                    button.textContent = 'Copied';
                    setTimeout(() => {
                        button.textContent = original;
                    }, 1500);
                } catch (error) {
                    button.textContent = 'Failed';
                    setTimeout(() => {
                        button.textContent = original;
                    }, 1500);
                    console.error('Failed to copy text', error);
                }
            });
        });
    }

    setTheme(storedTheme === 'light' || storedTheme === 'dark' ? storedTheme : 'dark');

    if (themeToggle) {
        themeToggle.addEventListener('click', () => {
            setTheme(getEffectiveTheme() === 'dark' ? 'light' : 'dark');
        });
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
