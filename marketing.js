// Marketing page JavaScript
document.addEventListener('DOMContentLoaded', function() {
    // Smooth scrolling for navigation links
    const navLinks = document.querySelectorAll('.nav-links a[href^="#"]');
    navLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            const targetId = this.getAttribute('href');
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                const headerHeight = document.querySelector('.nav').offsetHeight;
                const targetPosition = targetElement.offsetTop - headerHeight - 20;
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });

    // Intersection Observer for animations
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
            }
        });
    }, observerOptions);

    // Observe elements for animation
    const animatedElements = document.querySelectorAll('.feature-card, .install-step, .command-example');
    animatedElements.forEach(el => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(30px)';
        observer.observe(el);
    });

    // Terminal typing animation
    const terminalLines = document.querySelectorAll('.terminal-line');
    let currentLine = 0;
    let currentChar = 0;

    function typeWriter() {
        if (currentLine < terminalLines.length) {
            const line = terminalLines[currentLine];
            const text = line.textContent;
            line.textContent = '';

            function typeChar() {
                if (currentChar < text.length) {
                    line.textContent += text.charAt(currentChar);
                    currentChar++;
                    setTimeout(typeChar, 50);
                } else {
                    currentLine++;
                    currentChar = 0;
                    setTimeout(typeWriter, 500);
                }
            }

            typeChar();
        }
    }

    // Start typing animation after a delay
    setTimeout(typeWriter, 1000);

    // Add scroll effect to navigation
    let lastScrollTop = 0;
    const nav = document.querySelector('.nav');

    window.addEventListener('scroll', () => {
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

        if (scrollTop > lastScrollTop && scrollTop > 100) {
            // Scrolling down
            nav.style.transform = 'translateY(-100%)';
        } else {
            // Scrolling up
            nav.style.transform = 'translateY(0)';
        }

        // Add background blur on scroll
        if (scrollTop > 50) {
            nav.style.background = 'rgba(10, 10, 10, 0.98)';
            nav.style.backdropFilter = 'blur(20px)';
        } else {
            nav.style.background = 'rgba(10, 10, 10, 0.95)';
            nav.style.backdropFilter = 'blur(10px)';
        }

        lastScrollTop = scrollTop <= 0 ? 0 : scrollTop;
    });

    // Platform download buttons - add click handlers
    const platformBtns = document.querySelectorAll('.platform-btn');
    platformBtns.forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            // In a real implementation, these would link to actual download URLs
            alert(`Download link for ${btn.textContent} would be here!`);
        });
    });

    // Add particle effect to hero background (subtle)
    const hero = document.querySelector('.hero');
    for (let i = 0; i < 20; i++) {
        const particle = document.createElement('div');
        particle.className = 'particle';
        particle.style.left = Math.random() * 100 + '%';
        particle.style.top = Math.random() * 100 + '%';
        particle.style.animationDelay = Math.random() * 10 + 's';
        particle.style.animationDuration = (Math.random() * 10 + 10) + 's';
        hero.appendChild(particle);
    }
});

// Add CSS for particles
const style = document.createElement('style');
style.textContent = `
    .particle {
        position: absolute;
        width: 2px;
        height: 2px;
        background: rgba(0, 212, 255, 0.3);
        border-radius: 50%;
        pointer-events: none;
        animation: float 20s linear infinite;
    }

    @keyframes float {
        0% {
            transform: translateY(100vh) rotate(0deg);
            opacity: 0;
        }
        10% {
            opacity: 1;
        }
        90% {
            opacity: 1;
        }
        100% {
            transform: translateY(-100vh) rotate(360deg);
            opacity: 0;
        }
    }
`;
document.head.appendChild(style);