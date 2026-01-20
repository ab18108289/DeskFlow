// ============================================
// DeskFlow 官网脚本
// ============================================

// 导航栏滚动效果
const navbar = document.querySelector('.navbar');
let lastScroll = 0;

window.addEventListener('scroll', () => {
    const currentScroll = window.scrollY;
    
    if (currentScroll > 50) {
        navbar.style.background = 'rgba(9, 9, 11, 0.95)';
    } else {
        navbar.style.background = 'rgba(9, 9, 11, 0.8)';
    }
    
    lastScroll = currentScroll;
});

// 平滑滚动到锚点
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        e.preventDefault();
        const target = document.querySelector(this.getAttribute('href'));
        if (target) {
            const navHeight = navbar.offsetHeight;
            const targetPosition = target.getBoundingClientRect().top + window.scrollY - navHeight - 20;
            
            window.scrollTo({
                top: targetPosition,
                behavior: 'smooth'
            });
        }
    });
});

// 元素进入视口动画
const observerOptions = {
    threshold: 0.1,
    rootMargin: '0px 0px -50px 0px'
};

const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.classList.add('animate-in');
        }
    });
}, observerOptions);

// 添加动画类
document.querySelectorAll('.feature-card, .highlight-card, .changelog-item, .download-card').forEach((el, index) => {
    el.style.opacity = '0';
    el.style.transform = 'translateY(20px)';
    el.style.transition = `all 0.5s ease ${index * 0.05}s`;
    observer.observe(el);
});

// 监听动画类
const style = document.createElement('style');
style.textContent = `
    .animate-in {
        opacity: 1 !important;
        transform: translateY(0) !important;
    }
`;
document.head.appendChild(style);

// 待办列表交互动画
const todoItems = document.querySelectorAll('.todo');
todoItems.forEach((todo, index) => {
    todo.style.opacity = '0';
    todo.style.transform = 'translateX(-20px)';
    
    setTimeout(() => {
        todo.style.transition = 'all 0.4s ease';
        todo.style.opacity = '1';
        todo.style.transform = 'translateX(0)';
    }, 500 + index * 150);
});

// 侧边栏项目点击效果
const sidebarItems = document.querySelectorAll('.sidebar-item');
sidebarItems.forEach(item => {
    item.addEventListener('click', () => {
        sidebarItems.forEach(i => i.classList.remove('active'));
        item.classList.add('active');
    });
});

// 滚动提示点击
const scrollHint = document.querySelector('.scroll-hint');
if (scrollHint) {
    scrollHint.addEventListener('click', () => {
        const highlights = document.querySelector('.highlights');
        if (highlights) {
            highlights.scrollIntoView({ behavior: 'smooth' });
        }
    });
    scrollHint.style.cursor = 'pointer';
}

// 打字机效果（可选）
function typeWriter(element, text, speed = 50) {
    let i = 0;
    element.textContent = '';
    
    function type() {
        if (i < text.length) {
            element.textContent += text.charAt(i);
            i++;
            setTimeout(type, speed);
        }
    }
    
    type();
}

// 数字递增动画
function animateNumber(element, target, duration = 1000) {
    const start = 0;
    const increment = target / (duration / 16);
    let current = start;
    
    function update() {
        current += increment;
        if (current < target) {
            element.textContent = Math.floor(current);
            requestAnimationFrame(update);
        } else {
            element.textContent = target;
        }
    }
    
    update();
}

// 控制台欢迎信息
console.log('%c📅 DeskFlow', 'font-size: 28px; font-weight: bold; color: #8b5cf6;');
console.log('%c让每一天井井有条', 'font-size: 14px; color: #a1a1aa; margin-top: 8px;');
console.log('%c⭐ Star us on GitHub: https://github.com/ab18108289/DeskFlow', 'font-size: 12px; color: #a78bfa;');

// 复制密码功能
document.querySelectorAll('.download-card').forEach(card => {
    const desc = card.querySelector('.download-desc');
    if (desc && desc.textContent.includes('密码')) {
        card.addEventListener('click', (e) => {
            // 如果点击的是蓝奏云链接，复制密码到剪贴板
            if (desc.textContent.includes('2rax')) {
                navigator.clipboard.writeText('2rax').then(() => {
                    const original = desc.textContent;
                    desc.textContent = '✓ 密码已复制！';
                    setTimeout(() => {
                        desc.textContent = original;
                    }, 2000);
                });
            }
        });
    }
});

// 页面加载完成后的动画
window.addEventListener('load', () => {
    document.body.classList.add('loaded');
    
    // Hero 内容动画
    const heroContent = document.querySelector('.hero-content');
    const heroProduct = document.querySelector('.hero-product');
    
    if (heroContent) {
        heroContent.style.opacity = '0';
        heroContent.style.transform = 'translateY(30px)';
        
        setTimeout(() => {
            heroContent.style.transition = 'all 0.8s ease';
            heroContent.style.opacity = '1';
            heroContent.style.transform = 'translateY(0)';
        }, 100);
    }
    
    if (heroProduct) {
        heroProduct.style.opacity = '0';
        heroProduct.style.transform = 'translateY(30px)';
        
        setTimeout(() => {
            heroProduct.style.transition = 'all 0.8s ease';
            heroProduct.style.opacity = '1';
            heroProduct.style.transform = 'translateY(0)';
        }, 300);
    }
});
