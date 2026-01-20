// ============================================
// DeskFlow 官网脚本
// ============================================

// 导航栏滚动效果
const navbar = document.querySelector('.navbar');

window.addEventListener('scroll', () => {
    if (window.scrollY > 50) {
        navbar.style.background = 'rgba(15, 15, 26, 0.95)';
    } else {
        navbar.style.background = 'rgba(15, 15, 26, 0.8)';
    }
});

// 平滑滚动到锚点
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        e.preventDefault();
        const target = document.querySelector(this.getAttribute('href'));
        if (target) {
            target.scrollIntoView({
                behavior: 'smooth',
                block: 'start'
            });
        }
    });
});

// 功能卡片动画
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

// 观察功能卡片
document.querySelectorAll('.feature-card').forEach((card, index) => {
    card.style.opacity = '0';
    card.style.transform = 'translateY(30px)';
    card.style.transition = `all 0.5s ease ${index * 0.1}s`;
    observer.observe(card);
});

// 观察更新日志项
document.querySelectorAll('.changelog-item').forEach((item, index) => {
    item.style.opacity = '0';
    item.style.transform = 'translateX(-20px)';
    item.style.transition = `all 0.5s ease ${index * 0.15}s`;
    observer.observe(item);
});

// App Preview 悬浮效果增强
const appPreview = document.querySelector('.app-preview');
if (appPreview) {
    appPreview.addEventListener('mousemove', (e) => {
        const rect = appPreview.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;
        const centerX = rect.width / 2;
        const centerY = rect.height / 2;
        const rotateX = (y - centerY) / 20;
        const rotateY = (centerX - x) / 20;
        
        appPreview.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg)`;
    });
    
    appPreview.addEventListener('mouseleave', () => {
        appPreview.style.transform = 'perspective(1000px) rotateX(0) rotateY(0)';
    });
}

// 控制台欢迎信息
console.log('%c🗓️ DeskFlow', 'font-size: 24px; font-weight: bold; color: #6366f1;');
console.log('%c让每一天井井有条', 'font-size: 14px; color: #a1a1aa;');
console.log('%cGitHub: https://github.com/ab18108289/DeskFlow', 'font-size: 12px; color: #818cf8;');

