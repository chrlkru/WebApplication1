(() => {
  const pointerLayer = document.getElementById("bgPointer");
  let currentX = 0;
  let currentY = 0;
  let targetX = 0;
  let targetY = 0;

  const glowCards = Array.from(document.querySelectorAll("[data-glow]"));

  const syncGlowCards = (x, y) => {
    const xp = (x / window.innerWidth).toFixed(3);
    const yp = (y / window.innerHeight).toFixed(3);
    const xFixed = x.toFixed(2);
    const yFixed = y.toFixed(2);

    for (const card of glowCards) {
      card.style.setProperty("--x", xFixed);
      card.style.setProperty("--y", yFixed);
      card.style.setProperty("--xp", xp);
      card.style.setProperty("--yp", yp);
    }
  };

  const movePointer = () => {
    if (pointerLayer) {
      currentX += (targetX - currentX) / 20;
      currentY += (targetY - currentY) / 20;
      pointerLayer.style.transform = `translate(${Math.round(currentX)}px, ${Math.round(currentY)}px)`;
    }

    requestAnimationFrame(movePointer);
  };

  window.addEventListener("pointermove", (event) => {
    targetX = event.clientX;
    targetY = event.clientY;
    syncGlowCards(event.clientX, event.clientY);
  });

  const initShowcaseCarousels = () => {
    const carousels = Array.from(document.querySelectorAll("[data-showcase-carousel]"));
    for (const carousel of carousels) {
      const slides = Array.from(carousel.querySelectorAll("[data-showcase-slide]"));
      const track = carousel.querySelector("[data-showcase-track]");
      const prevButton = carousel.querySelector("[data-showcase-prev]");
      const nextButton = carousel.querySelector("[data-showcase-next]");
      if (slides.length === 0 || !track) {
        continue;
      }

      const slideCount = slides.length;
      let current = 0;
      let positions = [];

      const computePositions = () => {
        const firstOffset = slides[0].offsetLeft;
        positions = slides.map((slide) => Math.max(0, slide.offsetLeft - firstOffset));
      };

      const applyState = () => {
        if (positions.length !== slideCount) {
          computePositions();
        }

        const x = positions[current] ?? 0;
        track.style.transition = "transform 1s ease";
        track.style.transform = `translateX(-${x}px)`;
        slides.forEach((slide, index) => {
          slide.classList.toggle("is-active", index === current);
        });
      };

      const moveTo = (index) => {
        current = index;
        if (current < 0) {
          current = slideCount - 1;
        } else if (current >= slideCount) {
          current = 0;
        }

        applyState();
      };

      prevButton?.addEventListener("click", () => moveTo(current - 1));
      nextButton?.addEventListener("click", () => moveTo(current + 1));

      slides.forEach((slide, index) => {
        let x = 0;
        let y = 0;
        let tx = 0;
        let ty = 0;

        const animate = () => {
          x += (tx - x) / 6;
          y += (ty - y) / 6;
          slide.style.setProperty("--slide-x", `${x.toFixed(2)}px`);
          slide.style.setProperty("--slide-y", `${y.toFixed(2)}px`);
          window.requestAnimationFrame(animate);
        };

        const onMouseMove = (event) => {
          const rect = slide.getBoundingClientRect();
          tx = event.clientX - (rect.left + rect.width / 2);
          ty = event.clientY - (rect.top + rect.height / 2);
        };

        const onMouseLeave = () => {
          tx = 0;
          ty = 0;
        };

        slide.addEventListener("mousemove", onMouseMove);
        slide.addEventListener("mouseleave", onMouseLeave);
        slide.addEventListener("click", () => {
          const rawIndex = slide.getAttribute("data-slide-index");
          const logicalIndex = Number(rawIndex);
          if (Number.isNaN(logicalIndex)) {
            return;
          }

          if (current !== logicalIndex) {
            moveTo(logicalIndex);
          }
        });
        window.requestAnimationFrame(animate);
      });

      computePositions();
      applyState();
      window.addEventListener("resize", () => {
        computePositions();
        applyState();
      });
    }
  };

  initShowcaseCarousels();
  requestAnimationFrame(movePointer);
})();
