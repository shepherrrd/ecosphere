"use client";

import { useEffect, useRef } from "react";

export default function SpaceBackground() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const mousePos = useRef({ x: 0, y: 0 });
  const stars = useRef<Array<{
    x: number;
    y: number;
    radius: number;
    vx: number;
    vy: number;
    originalX: number;
    originalY: number;
  }>>([]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    // Set canvas size
    const resizeCanvas = () => {
      canvas.width = window.innerWidth;
      canvas.height = window.innerHeight;
      initStars();
    };

    // Initialize stars
    const initStars = () => {
      stars.current = [];
      const starCount = Math.floor((canvas.width * canvas.height) / 3000);

      for (let i = 0; i < starCount; i++) {
        const x = Math.random() * canvas.width;
        const y = Math.random() * canvas.height;
        stars.current.push({
          x,
          y,
          radius: Math.random() * 1.5 + 0.5,
          vx: 0,
          vy: 0,
          originalX: x,
          originalY: y,
        });
      }
    };

    // Mouse move handler
    const handleMouseMove = (e: MouseEvent) => {
      mousePos.current = { x: e.clientX, y: e.clientY };
    };

    // Animation loop
    const animate = () => {
      ctx.fillStyle = "rgba(0, 0, 0, 0.1)";
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      stars.current.forEach((star) => {
        // Calculate distance from mouse
        const dx = mousePos.current.x - star.x;
        const dy = mousePos.current.y - star.y;
        const distance = Math.sqrt(dx * dx + dy * dy);
        const maxDistance = 200;

        if (distance < maxDistance) {
          // Push stars away from mouse
          const force = (maxDistance - distance) / maxDistance;
          const angle = Math.atan2(dy, dx);
          star.vx -= Math.cos(angle) * force * 0.5;
          star.vy -= Math.sin(angle) * force * 0.5;
        }

        // Return to original position
        star.vx += (star.originalX - star.x) * 0.01;
        star.vy += (star.originalY - star.y) * 0.01;

        // Apply friction
        star.vx *= 0.95;
        star.vy *= 0.95;

        // Update position
        star.x += star.vx;
        star.y += star.vy;

        // Draw star
        ctx.beginPath();
        ctx.arc(star.x, star.y, star.radius, 0, Math.PI * 2);

        // Varying opacity based on radius
        const opacity = 0.5 + (star.radius / 2) * 0.5;
        ctx.fillStyle = `rgba(255, 255, 255, ${opacity})`;
        ctx.fill();

        // Draw connections to nearby stars
        stars.current.forEach((otherStar) => {
          const dx = star.x - otherStar.x;
          const dy = star.y - otherStar.y;
          const distance = Math.sqrt(dx * dx + dy * dy);

          if (distance < 100 && distance > 0) {
            ctx.beginPath();
            ctx.moveTo(star.x, star.y);
            ctx.lineTo(otherStar.x, otherStar.y);
            const lineOpacity = (1 - distance / 100) * 0.15;
            ctx.strokeStyle = `rgba(100, 150, 255, ${lineOpacity})`;
            ctx.lineWidth = 0.5;
            ctx.stroke();
          }
        });
      });

      requestAnimationFrame(animate);
    };

    resizeCanvas();
    window.addEventListener("resize", resizeCanvas);
    window.addEventListener("mousemove", handleMouseMove);
    animate();

    return () => {
      window.removeEventListener("resize", resizeCanvas);
      window.removeEventListener("mousemove", handleMouseMove);
    };
  }, []);

  return (
    <canvas
      ref={canvasRef}
      className="fixed inset-0 w-full h-full"
      style={{ background: "linear-gradient(to bottom, #000000, #0a0a1a)" }}
    />
  );
}
