/* ═══════════════════════════════════════════════════════════
   ASPS — main.js
   Animations, Canvas, Interactions
   ═══════════════════════════════════════════════════════════ */

'use strict';

/* ──────────────────────────────── UTILS */
const $ = (sel, ctx = document) => ctx.querySelector(sel);
const $$ = (sel, ctx = document) => [...ctx.querySelectorAll(sel)];

/* ──────────────────────────────── PRELOADER */
window.addEventListener('load', () => {
  const preloader = $('#preloader');
  setTimeout(() => {
    gsap.to(preloader, {
      opacity: 0,
      duration: 0.6,
      ease: 'power2.inOut',
      onComplete: () => {
        preloader.style.display = 'none';
        initAnimations();
      }
    });
  }, 1200);
});

/* ──────────────────────────────── CANVAS PARTICLES */
function initCanvas() {
  const canvas = $('#heroCanvas');
  if (!canvas) return;
  const ctx = canvas.getContext('2d');

  let w, h, particles = [];

  const COLORS = ['rgba(0,212,255,', 'rgba(124,58,237,', 'rgba(0,212,255,'];
  const COUNT  = 80;

  function resize() {
    w = canvas.width  = window.innerWidth;
    h = canvas.height = canvas.parentElement.offsetHeight;
  }

  class Particle {
    constructor() { this.reset(true); }

    reset(initial = false) {
      this.x    = Math.random() * w;
      this.y    = initial ? Math.random() * h : h + 10;
      this.r    = Math.random() * 2 + 0.5;
      this.vx   = (Math.random() - 0.5) * 0.4;
      this.vy   = -(Math.random() * 0.5 + 0.2);
      this.a    = Math.random() * 0.6 + 0.1;
      this.col  = COLORS[Math.floor(Math.random() * COLORS.length)];
      this.life = 0;
      this.maxL = Math.random() * 200 + 100;
    }

    update() {
      this.x += this.vx;
      this.y += this.vy;
      this.life++;
      if (this.y < -10 || this.life > this.maxL) this.reset();
    }

    draw() {
      const alpha = Math.sin((this.life / this.maxL) * Math.PI) * this.a;
      ctx.beginPath();
      ctx.arc(this.x, this.y, this.r, 0, Math.PI * 2);
      ctx.fillStyle = this.col + alpha + ')';
      ctx.fill();
    }
  }

  // Grid lines (AI scanner feel)
  function drawGrid() {
    const step = 80;
    ctx.strokeStyle = 'rgba(0,212,255,0.03)';
    ctx.lineWidth   = 1;
    for (let x = 0; x < w; x += step) {
      ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke();
    }
    for (let y = 0; y < h; y += step) {
      ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke();
    }
  }

  // Connection lines between close particles
  function drawConnections() {
    for (let i = 0; i < particles.length; i++) {
      for (let j = i + 1; j < particles.length; j++) {
        const dx = particles[i].x - particles[j].x;
        const dy = particles[i].y - particles[j].y;
        const d  = Math.sqrt(dx * dx + dy * dy);
        if (d < 120) {
          ctx.beginPath();
          ctx.moveTo(particles[i].x, particles[i].y);
          ctx.lineTo(particles[j].x, particles[j].y);
          ctx.strokeStyle = `rgba(0,212,255,${(1 - d / 120) * 0.08})`;
          ctx.lineWidth   = 0.5;
          ctx.stroke();
        }
      }
    }
  }

  function loop() {
    ctx.clearRect(0, 0, w, h);
    drawGrid();
    drawConnections();
    particles.forEach(p => { p.update(); p.draw(); });
    requestAnimationFrame(loop);
  }

  resize();
  for (let i = 0; i < COUNT; i++) particles.push(new Particle());
  window.addEventListener('resize', resize);
  loop();
}

/* ──────────────────────────────── NAVBAR */
function initNavbar() {
  const navbar = $('#navbar');
  const hamburger = $('#hamburger');
  const mobileMenu = $('#mobile-menu');

  window.addEventListener('scroll', () => {
    navbar.classList.toggle('scrolled', window.scrollY > 50);
  });

  hamburger?.addEventListener('click', () => {
    mobileMenu.classList.toggle('open');
  });

  // Close mobile menu on link click
  $$('.mobile-menu a').forEach(a => {
    a.addEventListener('click', () => mobileMenu.classList.remove('open'));
  });

  // Smooth scroll for all anchor links
  $$('a[href^="#"]').forEach(a => {
    a.addEventListener('click', e => {
      const target = $(a.getAttribute('href'));
      if (!target) return;
      e.preventDefault();
      target.scrollIntoView({ behavior: 'smooth' });
    });
  });
}

/* ──────────────────────────────── GSAP ANIMATIONS */
function initAnimations() {
  gsap.registerPlugin(ScrollTrigger);

  // Hero entrance
  const heroItems = $$('.hero-content .reveal-up');
  gsap.fromTo(heroItems, 
    { opacity: 0, y: 40 },
    { opacity: 1, y: 0, duration: 0.8, stagger: 0.15, ease: 'power3.out', delay: 0.2 }
  );

  // Shield entrance
  gsap.fromTo('.hero-shield',
    { opacity: 0, scale: 0.6 },
    { opacity: 0.7, scale: 1, duration: 1.2, ease: 'back.out(1.4)', delay: 0.4 }
  );

  // Generic scroll reveals
  $$('.reveal-up').forEach(el => {
    const delay = parseFloat(el.dataset.delay || 0);
    ScrollTrigger.create({
      trigger: el,
      start: 'top 88%',
      onEnter: () => {
        gsap.to(el, {
          opacity: 1,
          y: 0,
          duration: 0.7,
          delay,
          ease: 'power3.out'
        });
      }
    });
  });
}

/* ──────────────────────────────── COUNTER ANIMATION */
function initCounters() {
  $$('.stat-number').forEach(el => {
    const target = parseFloat(el.dataset.target);
    const prefix = el.dataset.prefix || '';
    const suffix = el.dataset.suffix || '';
    let started  = false;

    const observer = new IntersectionObserver(entries => {
      if (!entries[0].isIntersecting || started) return;
      started = true;

      const duration = 2000;
      const start    = performance.now();
      const isInt    = Number.isInteger(target);

      function step(now) {
        const t        = Math.min((now - start) / duration, 1);
        const eased    = 1 - Math.pow(1 - t, 3); // ease-out cubic
        const current  = target * eased;
        el.textContent = prefix + (isInt ? Math.round(current) : current.toFixed(1)) + suffix;
        if (t < 1) requestAnimationFrame(step);
      }
      requestAnimationFrame(step);
    }, { threshold: 0.5 });

    observer.observe(el);
  });
}

/* ──────────────────────────────── CUSTOM CURSOR */
function initCursor() {
  const dot  = $('#cursorDot');
  const ring = $('#cursorRing');
  if (!dot || !ring) return;

  let mx = 0, my = 0;
  let rx = 0, ry = 0;

  document.addEventListener('mousemove', e => {
    mx = e.clientX;
    my = e.clientY;
    dot.style.left  = mx + 'px';
    dot.style.top   = my + 'px';
  });

  function animateRing() {
    rx += (mx - rx) * 0.12;
    ry += (my - ry) * 0.12;
    ring.style.left = rx + 'px';
    ring.style.top  = ry + 'px';
    requestAnimationFrame(animateRing);
  }
  animateRing();

  // Hover effects
  const interactives = $$('a, button, .scam-card, .adv-card, .team-card, .step, input');
  interactives.forEach(el => {
    el.addEventListener('mouseenter', () => {
      ring.style.width       = '50px';
      ring.style.height      = '50px';
      ring.style.borderColor = 'rgba(0,212,255,0.8)';
    });
    el.addEventListener('mouseleave', () => {
      ring.style.width       = '30px';
      ring.style.height      = '30px';
      ring.style.borderColor = 'rgba(0,212,255,0.5)';
    });
  });
}

/* ──────────────────────────────── THREAT VECTOR ANIMATION */
function initThreatVectors() {
  const vectors = $$('.threat-vector');
  if (!vectors.length) return;

  vectors.forEach((v, i) => {
    gsap.to(v, {
      opacity: 0.3,
      duration: 1.5,
      repeat: -1,
      yoyo: true,
      delay: i * 0.25,
      ease: 'power1.inOut'
    });
  });
}

/* ──────────────────────────────── WAITLIST FORM */
function initForm() {
  const form    = $('#waitlistForm');
  const success = $('#formSuccess');
  if (!form) return;

  form.addEventListener('submit', async e => {
    e.preventDefault();

    const btnText    = form.querySelector('.form-btn-text');
    const btnLoading = form.querySelector('.form-btn-loading');

    btnText.style.display    = 'none';
    btnLoading.style.display = 'inline';

    try {
      const data = new FormData(form);
      const res  = await fetch(form.action, {
        method: 'POST',
        body: data,
        headers: { Accept: 'application/json' }
      });

      if (res.ok) {
        gsap.to(form, {
          opacity: 0, y: -20, duration: 0.4,
          onComplete: () => {
            form.style.display    = 'none';
            success.style.display = 'block';
            gsap.fromTo(success, { opacity: 0, y: 20 }, { opacity: 1, y: 0, duration: 0.5 });
          }
        });
      } else {
        throw new Error('Form submission failed');
      }
    } catch {
      btnText.style.display    = 'inline';
      btnLoading.style.display = 'none';
      alert('Something went wrong. Please try again.');
    }
  });
}

/* ──────────────────────────────── PARALLAX */
function initParallax() {
  window.addEventListener('scroll', () => {
    const y = window.scrollY;
    const heroContent = $('.hero-content');
    if (heroContent) {
      heroContent.style.transform = `translateY(${y * 0.3}px)`;
      heroContent.style.opacity   = 1 - y / 600;
    }
  });
}

/* ──────────────────────────────── CARD GLOW TRACKING */
function initCardGlow() {
  $$('.scam-card, .adv-card, .team-card').forEach(card => {
    card.addEventListener('mousemove', e => {
      const rect = card.getBoundingClientRect();
      const x    = ((e.clientX - rect.left) / rect.width  * 100).toFixed(1);
      const y    = ((e.clientY - rect.top)  / rect.height * 100).toFixed(1);
      card.style.background = `radial-gradient(circle at ${x}% ${y}%, rgba(0,212,255,0.06), rgba(255,255,255,0.02))`;
    });
    card.addEventListener('mouseleave', () => {
      card.style.background = '';
    });
  });
}

/* ──────────────────────────────── ACTIVE NAV LINK */
function initActiveNav() {
  const sections = $$('section[id]');
  const links    = $$('.nav-links a');

  window.addEventListener('scroll', () => {
    const y = window.scrollY + 120;
    sections.forEach(s => {
      if (y >= s.offsetTop && y < s.offsetTop + s.offsetHeight) {
        links.forEach(l => l.classList.remove('active'));
        const active = links.find(l => l.getAttribute('href') === '#' + s.id);
        if (active) active.classList.add('active');
      }
    });
  });
}

/* ──────────────────────────────── IMAGE PICKER (DEV ONLY) */
function initImagePicker() {
  // Hero background picker
  const heroEl   = $('#hero');
  const thumbs   = $$('.ip-thumb');

  // Create background image layer inside hero
  const bgLayer  = document.createElement('div');
  bgLayer.className = 'hero-bg-img';
  heroEl.insertBefore(bgLayer, heroEl.firstChild);

  thumbs.forEach(thumb => {
    thumb.addEventListener('click', () => {
      thumbs.forEach(t => t.classList.remove('active'));
      thumb.classList.add('active');

      const img = thumb.dataset.img;
      if (img === 'none') {
        heroEl.classList.remove('has-bg-img');
        bgLayer.style.backgroundImage = '';
      } else {
        heroEl.classList.add('has-bg-img');
        bgLayer.style.backgroundImage = `url('${img}')`;
      }
    });
  });

  // Feature image switcher
  const fiOptions = $$('.fi-option');

  fiOptions.forEach(opt => {
    opt.addEventListener('click', () => {
      const target = $('#' + opt.dataset.target);
      if (!target) return;

      const src = opt.querySelector('img')?.src
        ?.replace('w=800', 'w=1200')
        ?.replace('q=80', 'q=85');
      if (!src) return;

      gsap.to(target, {
        opacity: 0, duration: 0.25,
        onComplete: () => {
          target.src = src;
          gsap.to(target, { opacity: 1, duration: 0.4 });
        }
      });

      fiOptions.forEach(o => o.classList.remove('active'));
      opt.classList.add('active');
    });
  });
}

/* ──────────────────────────────── INIT */
document.addEventListener('DOMContentLoaded', () => {
  initCanvas();
  initNavbar();
  initCounters();
  initCursor();
  initThreatVectors();
  initForm();
  initParallax();
  initCardGlow();
  initActiveNav();
  initImagePicker();
});
