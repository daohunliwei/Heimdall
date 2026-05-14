'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { FaArrowLeft, FaSync, FaDownload, FaArrowRight, FaArrowUp, FaTimes } from 'react-icons/fa';
import ThemeToggle from '@/components/theme-toggle';
import { useLanguage } from '@/contexts/LanguageContext';
import { buildTaskRequestBody } from '@/utils/taskRequest';

interface Slide {
  id: string;
  title: string;
  content: string;
  html: string;
}

interface SlidesTaskResponse {
  plan: string;
  slides: Slide[];
}

export default function SlidesPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const repositoryId = params.repositoryId as string;
  const providerParam = searchParams.get('provider') || '';
  const modelParam = searchParams.get('model') || '';
  const isCustomModelParam = searchParams.get('is_custom_model') === 'true';
  const customModelParam = searchParams.get('custom_model') || '';
  const language = searchParams.get('language') || 'zh';
  const { messages } = useLanguage();

  const [repo, setRepo] = useState<string>('');
  const [isLoading, setIsLoading] = useState(false);
  const [loadingMessage, setLoadingMessage] = useState<string | undefined>(
    messages.loading?.initializing || '正在初始化演示文稿任务...'
  );
  const [error, setError] = useState<string | null>(null);
  const [slides, setSlides] = useState<Slide[]>([]);
  const [currentSlideIndex, setCurrentSlideIndex] = useState(0);
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const generateSlidesContent = useCallback(async () => {
    if (isLoading) return;

    setIsLoading(true);
    setError(null);
    setSlides([]);
    setCurrentSlideIndex(0);
    setLoadingMessage(messages.loading?.generatingSlides || '正在调用后端生成演示文稿...');

    try {
      const requestBody = buildTaskRequestBody({
        token: null,
        provider: providerParam,
        model: modelParam,
        isCustomModel: isCustomModelParam,
        customModel: customModelParam,
        language,
      }, { comprehensive: true });

      const bodyWithRepoId = { ...requestBody, repository_id: repositoryId };

      const response = await fetch('/api/tasks/slides', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(bodyWithRepoId),
      });

      if (!response.ok) {
        const errorBody = await response.json().catch(() => ({ error: '生成 Slides 失败' }));
        throw new Error(errorBody.error || `生成 Slides 失败：${response.status}`);
      }

      const data = await response.json() as SlidesTaskResponse;
      setSlides(data.slides || []);
    } catch (err) {
      console.error('Error generating slides content:', err);
      setError(err instanceof Error ? err.message : '生成 Slides 失败');
    } finally {
      setIsLoading(false);
      setLoadingMessage(undefined);
    }
  }, [providerParam, modelParam, isCustomModelParam, customModelParam, language, isLoading, messages.loading, repositoryId]);

  const exportSlides = useCallback(async () => {
    if (slides.length === 0) { setExportError('暂无可导出的演示文稿内容'); return; }
    try {
      setIsExporting(true); setExportError(null);
      const htmlContent = `<!DOCTYPE html><html lang="${language}"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"><title>${repo} Slides</title><link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.4.0/css/all.min.css"><style>body{font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;margin:0;padding:0;background-color:#0d1117;color:#e6edf3;}.slide-container{max-width:1280px;height:720px;margin:2rem auto;position:relative;overflow:hidden;box-shadow:0 10px 30px rgba(0,0,0,0.5);border-radius:8px;}@media print{.slide-container{page-break-after:always;margin:0;height:100vh;box-shadow:none;border-radius:0;}}.nav-controls{position:fixed;bottom:20px;left:50%;transform:translateX(-50%);display:flex;gap:20px;z-index:1000;background:rgba(13,17,23,0.8);padding:10px 20px;border-radius:30px;}.nav-btn{background:rgba(56,139,253,0.1);border:1px solid rgba(56,139,253,0.4);color:#58a6ff;border-radius:50%;width:40px;height:40px;display:flex;align-items:center;justify-content:center;cursor:pointer;font-size:18px;}.slide-indicator{display:flex;align-items:center;color:#8b949e;font-size:14px;}</style></head><body>${slides.map(slide => `<div class="slide-container">${slide.html}</div>`).join('\n')}<div class="nav-controls"><div class="nav-btn" onclick="prevSlide()"><i class="fas fa-chevron-left"></i></div><div class="slide-indicator"><span id="current-slide">1</span>/<span id="total-slides">${slides.length}</span></div><div class="nav-btn" onclick="nextSlide()"><i class="fas fa-chevron-right"></i></div></div><script>let currentSlide=1;const totalSlides=${slides.length};const slideContainers=document.querySelectorAll('.slide-container');function initSlides(){slideContainers.forEach((s,i)=>{s.style.display=i===0?'block':'none';});updateIndicator();}function showSlide(n){slideContainers.forEach((s,i)=>{s.style.display=i+1===n?'block':'none';});updateIndicator();}function nextSlide(){if(currentSlide<totalSlides){currentSlide++;showSlide(currentSlide);}}function prevSlide(){if(currentSlide>1){currentSlide--;showSlide(currentSlide);}}function updateIndicator(){document.getElementById('current-slide').textContent=currentSlide;}document.addEventListener('keydown',(e)=>{if(e.key==='ArrowRight'||e.key===' '){nextSlide();}else if(e.key==='ArrowLeft'){prevSlide();}});window.onload=function(){initSlides();};</script></body></html>`;
      const blob = new Blob([htmlContent], { type: 'text/html' });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a'); a.href = url; a.download = `${repo}_slides.html`;
      document.body.appendChild(a); a.click();
      window.URL.revokeObjectURL(url); document.body.removeChild(a);
    } catch (err) {
      console.error('Error exporting slides:', err);
      setExportError(err instanceof Error ? err.message : '导出演示文稿失败');
    } finally { setIsExporting(false); }
  }, [slides, repo, language]);

  const goToNextSlide = useCallback(() => { if (currentSlideIndex < slides.length - 1) setCurrentSlideIndex(prev => prev + 1); }, [currentSlideIndex, slides.length]);
  const goToPrevSlide = useCallback(() => { if (currentSlideIndex > 0) setCurrentSlideIndex(prev => prev - 1); }, [currentSlideIndex]);
  const toggleFullscreen = useCallback(() => { setIsFullscreen(prev => !prev); }, []);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'ArrowRight' || e.key === 'Space') goToNextSlide();
      else if (e.key === 'ArrowLeft') goToPrevSlide();
      else if (e.key === 'f' || e.key === 'F') toggleFullscreen();
      else if (e.key === 'Escape' && isFullscreen) setIsFullscreen(false);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [goToNextSlide, goToPrevSlide, toggleFullscreen, isFullscreen]);

  useEffect(() => {
    const loadRepo = async () => {
      try {
        const resp = await fetch(`/api/repositories/${repositoryId}`);
        if (resp.ok) {
          const detail = await resp.json();
          setRepo(detail.repo_name || detail.display_name || repositoryId);
        }
      } catch { setRepo(repositoryId); }
    };
    loadRepo();
  }, [repositoryId]);

  const contentGeneratedRef = useRef(false);
  useEffect(() => {
    if (!contentGeneratedRef.current) { contentGeneratedRef.current = true; generateSlidesContent(); }
  }, [generateSlidesContent]);

  return (
    <div className={`min-h-screen flex flex-col ${isFullscreen ? 'fixed inset-0 z-50 bg-[#0d1117]' : 'bg-[var(--background)]'}`}>
      {!isFullscreen && (
        <header className="sticky top-0 z-10 bg-[var(--card-bg)] border-b border-[var(--border-color)] shadow-sm">
          <div className="container mx-auto px-4 py-3 flex items-center justify-between">
            <div className="flex items-center space-x-4">
              <Link href={`/repositories/${repositoryId}`} className="flex items-center text-[var(--foreground)] hover:text-[var(--accent-primary)] transition-colors">
                <FaArrowLeft className="mr-2" />
                <span>{messages.slides?.backToWiki || 'Back to Wiki'}</span>
              </Link>
              <h1 className="text-xl font-bold text-[var(--accent-primary)]">
                {messages.slides?.title || 'Slides'}: {repo}
              </h1>
            </div>
            <div className="flex items-center space-x-3">
              <button onClick={generateSlidesContent} disabled={isLoading}
                className={`p-2 rounded-md ${isLoading ? 'bg-[var(--button-disabled-bg)] text-[var(--button-disabled-text)]' : 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] hover:bg-[var(--accent-primary)]/20'} transition-colors`}
                title={messages.slides?.regenerate || 'Regenerate Slides'}>
                <FaSync className={`${isLoading ? 'animate-spin' : ''}`} />
              </button>
              <button onClick={exportSlides} disabled={!slides.length || isExporting}
                className={`p-2 rounded-md ${!slides.length || isExporting ? 'bg-[var(--button-disabled-bg)] text-[var(--button-disabled-text)]' : 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] hover:bg-[var(--accent-primary)]/20'} transition-colors`}>
                <FaDownload />
              </button>
              <button onClick={toggleFullscreen} className="p-2 rounded-md bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] hover:bg-[var(--accent-primary)]/20 transition-colors">
                <FaArrowUp />
              </button>
              <ThemeToggle />
            </div>
          </div>
        </header>
      )}
      <main className={`flex-1 flex flex-col ${isFullscreen ? 'p-0' : 'container mx-auto px-4 py-6'}`}>
        {isLoading && !slides.length ? (
          <div className="flex flex-col items-center justify-center p-8 flex-grow">
            <div className="w-12 h-12 border-4 border-[var(--accent-primary)]/30 border-t-[var(--accent-primary)] rounded-full animate-spin mb-4"></div>
            <p className="text-[var(--foreground)]">{loadingMessage}</p>
          </div>
        ) : error ? (
          <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-md p-4 mb-6">
            <h3 className="text-red-800 dark:text-red-400 font-medium mb-2">{messages.common?.error || 'Error'}</h3>
            <p className="text-red-700 dark:text-red-300">{error}</p>
          </div>
        ) : slides.length > 0 ? (
          <div className="flex flex-col flex-grow">
            <div className={`flex-grow flex flex-col items-center justify-center ${isFullscreen ? 'p-0 bg-[#0d1117]' : 'bg-[var(--card-bg)] border border-[var(--border-color)] rounded-lg shadow-sm p-6 mb-4'}`}>
              {exportError && (
                <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-md p-3 mb-4 w-full">
                  <p className="text-red-700 dark:text-red-300 text-sm">{exportError}</p>
                </div>
              )}
              <div className={`${isFullscreen ? 'w-full h-full' : 'w-full max-w-[1280px] aspect-[16/9]'} flex items-center justify-center overflow-hidden`}>
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.4.0/css/all.min.css" />
                <div className="w-full h-full" dangerouslySetInnerHTML={{ __html: slides[currentSlideIndex]?.html || '' }} />
              </div>
            </div>
            <div className={`flex items-center justify-between ${isFullscreen ? 'fixed bottom-6 left-1/2 transform -translate-x-1/2 bg-[#0d1117]/80 px-6 py-3 rounded-full z-10 shadow-lg' : 'mt-4'}`}>
              <button onClick={goToPrevSlide} disabled={currentSlideIndex === 0}
                className={`p-2 rounded-md ${currentSlideIndex === 0 ? 'bg-[var(--button-disabled-bg)] text-[var(--button-disabled-text)]' : 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] hover:bg-[var(--accent-primary)]/20'} transition-colors`}>
                <FaArrowLeft />
              </button>
              <div className={`text-[var(--foreground)] ${isFullscreen ? 'mx-4' : ''}`}>Slide {currentSlideIndex + 1} of {slides.length}</div>
              <button onClick={goToNextSlide} disabled={currentSlideIndex === slides.length - 1}
                className={`p-2 rounded-md ${currentSlideIndex === slides.length - 1 ? 'bg-[var(--button-disabled-bg)] text-[var(--button-disabled-text)]' : 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] hover:bg-[var(--accent-primary)]/20'} transition-colors`}>
                <FaArrowRight />
              </button>
              {isFullscreen && (
                <button onClick={toggleFullscreen} className="p-2 ml-4 rounded-md bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] hover:bg-[var(--accent-primary)]/20 transition-colors">
                  <FaTimes />
                </button>
              )}
            </div>
          </div>
        ) : (
          <div className="flex flex-col items-center justify-center p-8 flex-grow">
            <p className="text-[var(--foreground)]">{messages.slides?.noSlides || 'No slides generated yet.'}</p>
          </div>
        )}
      </main>
    </div>
  );
}
