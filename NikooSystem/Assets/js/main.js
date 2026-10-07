/**
* Template Name: FlexStart - v1.6.0 (patched for Nikoooo)
*/
(function() {
  "use strict";

  const select = (el, all = false) => {
    if (!el) return all ? [] : null;
    el = el.trim();
    if (all) {
      return [...document.querySelectorAll(el)];
    }
    return document.querySelector(el);
  };

  const on = (type, el, listener, all = false) => {
    const elements = all ? select(el, true) : [select(el)];
    elements.filter(Boolean).forEach(e => e.addEventListener(type, listener));
  };

  const onscroll = (el, listener) => {
    el.addEventListener('scroll', listener);
  };

  let navbarlinks = select('#navbar .scrollto', true);
  const navbarlinksActive = () => {
    let position = window.scrollY + 200;
    navbarlinks.forEach(navbarlink => {
      if (!navbarlink.hash) return;
      let section = select(navbarlink.hash);
      if (!section) return;
      if (position >= section.offsetTop && position <= (section.offsetTop + section.offsetHeight)) {
        navbarlink.classList.add('active');
      } else {
        navbarlink.classList.remove('active');
      }
    });
  };
  window.addEventListener('load', navbarlinksActive);
  onscroll(document, navbarlinksActive);

  const scrollto = (el) => {
    let header = select('#header');
    let offset = header ? header.offsetHeight : 0;
    if (header && !header.classList.contains('header-scrolled')) {
      offset -= 10;
    }
    let elementPos = select(el).offsetTop;
    window.scrollTo({
      top: elementPos - offset,
      behavior: 'smooth'
    });
  };

  let selectHeader = select('#header');
  if (selectHeader) {
    const headerScrolled = () => {
      if (window.scrollY > 100) {
        selectHeader.classList.add('header-scrolled');
      } else {
        selectHeader.classList.remove('header-scrolled');
      }
    };
    window.addEventListener('load', headerScrolled);
    onscroll(document, headerScrolled);
  }

  let backtotop = select('.back-to-top');
  if (backtotop) {
    const toggleBacktotop = () => {
      if (window.scrollY > 100) {
        backtotop.classList.add('active');
      } else {
        backtotop.classList.remove('active');
      }
    };
    window.addEventListener('load', toggleBacktotop);
    onscroll(document, toggleBacktotop);
  }

  on('click', '.mobile-nav-toggle', function() {
    let navbar = select('#navbar');
    if (!navbar) return;
    navbar.classList.toggle('navbar-mobile');
    this.classList.toggle('bi-list');
    this.classList.toggle('bi-x');
  });

  on('click', '.navbar .dropdown > a', function(e) {
    if (select('#navbar') && select('#navbar').classList.contains('navbar-mobile')) {
      e.preventDefault();
      if (this.nextElementSibling) {
        this.nextElementSibling.classList.toggle('dropdown-active');
      }
    }
  }, true);

  on('click', '.scrollto', function(e) {
    if (this.hash && select(this.hash)) {
      e.preventDefault();
      let navbar = select('#navbar');
      if (navbar && navbar.classList.contains('navbar-mobile')) {
        navbar.classList.remove('navbar-mobile');
        let navbarToggle = select('.mobile-nav-toggle');
        if (navbarToggle) {
          navbarToggle.classList.toggle('bi-list');
          navbarToggle.classList.toggle('bi-x');
        }
      }
      scrollto(this.hash);
    }
  }, true);

  window.addEventListener('load', () => {
    if (window.location.hash && select(window.location.hash)) {
      scrollto(window.location.hash);
    }
  });

  if (typeof Swiper !== 'undefined' && select('.clients-slider')) {
    new Swiper('.clients-slider', {
      speed: 400,
      loop: true,
      autoplay: { delay: 5000, disableOnInteraction: false },
      slidesPerView: 'auto',
      pagination: { el: '.swiper-pagination', type: 'bullets', clickable: true },
      breakpoints: {
        320: { slidesPerView: 2, spaceBetween: 40 },
        480: { slidesPerView: 3, spaceBetween: 60 },
        640: { slidesPerView: 4, spaceBetween: 80 },
        992: { slidesPerView: 6, spaceBetween: 120 }
      }
    });
  }

  if (typeof Swiper !== 'undefined' && select('.portfolio-details-slider')) {
    new Swiper('.portfolio-details-slider', {
      speed: 400,
      autoplay: { delay: 5000, disableOnInteraction: false },
      pagination: { el: '.swiper-pagination', type: 'bullets', clickable: true }
    });
  }

  if (typeof Swiper !== 'undefined' && select('.testimonials-slider')) {
    new Swiper('.testimonials-slider', {
      speed: 600,
      loop: true,
      autoplay: { delay: 5000, disableOnInteraction: false },
      slidesPerView: 'auto',
      pagination: { el: '.swiper-pagination', type: 'bullets', clickable: true },
      breakpoints: {
        320: { slidesPerView: 1, spaceBetween: 40 },
        1200: { slidesPerView: 3 }
      }
    });
  }

  if (typeof GLightbox !== 'undefined' && select('.portfokio-lightbox')) {
    GLightbox({ selector: '.portfokio-lightbox' });
  }

  function aos_init() {
    if (typeof AOS !== 'undefined') {
      AOS.init({
        duration: 1000,
        easing: "ease-in-out",
        once: true,
        mirror: false
      });
    }
  }

  // Initialize ASAP so content is not stuck invisible
  if (document.readyState === 'complete') {
    aos_init();
  } else {
    window.addEventListener('load', aos_init);
    document.addEventListener('DOMContentLoaded', aos_init);
  }
})();
