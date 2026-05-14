'use client';

import React, { useState, useEffect, useMemo } from 'react';
import Link from 'next/link';
import { FaTimes, FaTh, FaList } from 'react-icons/fa';

// Interface should match the structure from the API
interface ProcessedProject {
  repository_id: string;
  id: string;
  owner: string;
  repo: string;
  name: string;
  display_name?: string;
  repo_type: string;
  submittedAt: number;
  language: string;
  default_branch?: string;
  latest_wiki_version_id?: string;
  published_wiki_version_id?: string;
}

interface ProcessedProjectsProps {
  showHeader?: boolean;
  maxItems?: number;
  className?: string;
  messages?: Record<string, Record<string, string>>; // Translation messages with proper typing
}

export default function ProcessedProjects({ 
  showHeader = true, 
  maxItems, 
  className = "",
  messages 
}: ProcessedProjectsProps) {
  const [projects, setProjects] = useState<ProcessedProject[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [viewMode, setViewMode] = useState<'card' | 'list'>('card');

  // Default messages fallback
  const defaultMessages = {
    title: 'Processed Wiki Projects',
    searchPlaceholder: 'Search projects by name, owner, or repository...',
    noProjects: 'No projects found in the server cache. The cache might be empty or the server encountered an issue.',
    noSearchResults: 'No projects match your search criteria.',
    processedOn: 'Processed on:',
    loadingProjects: 'Loading projects...',
    errorLoading: 'Error loading projects:',
    backToHome: 'Back to Home'
  };

  const t = (key: string) => {
    if (messages?.projects?.[key]) {
      return messages.projects[key];
    }
    return defaultMessages[key as keyof typeof defaultMessages] || key;
  };

  useEffect(() => {
    const fetchProjects = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const response = await fetch('/api/wiki/projects');
        if (!response.ok) {
          throw new Error(`Failed to fetch projects: ${response.statusText}`);
        }
        const data = await response.json();
        if (data.error) {
          throw new Error(data.error);
        }
        setProjects(data as ProcessedProject[]);
      } catch (e: unknown) {
        console.error("Failed to load projects from API:", e);
        const message = e instanceof Error ? e.message : "An unknown error occurred.";
        setError(message);
        setProjects([]);
      } finally {
        setIsLoading(false);
      }
    };

    fetchProjects();
  }, []);

  // Filter projects based on search query
  const filteredProjects = useMemo(() => {
    if (!searchQuery.trim()) {
      return maxItems ? projects.slice(0, maxItems) : projects;
    }

    const query = searchQuery.toLowerCase();
    const filtered = projects.filter(project => 
      project.name.toLowerCase().includes(query) ||
      project.owner.toLowerCase().includes(query) ||
      project.repo.toLowerCase().includes(query) ||
      project.repo_type.toLowerCase().includes(query)
    );

    return maxItems ? filtered.slice(0, maxItems) : filtered;
  }, [projects, searchQuery, maxItems]);

  const clearSearch = () => {
    setSearchQuery('');
  };

  const handleDelete = async (project: ProcessedProject) => {
    if (!confirm(`确定要删除项目 ${project.display_name || project.name} 吗？`)) {
      return;
    }
    try {
      const repoId = project.repository_id || project.id;
      const response = await fetch(`/api/processed_projects/${repoId}`, {
        method: 'DELETE',
      });
      if (!response.ok) {
        const errorBody = await response.json().catch(() => ({ error: response.statusText }));
        throw new Error(errorBody.error || response.statusText);
      }
      setProjects(prev => prev.filter(p => p.id !== project.id));
    } catch (e: unknown) {
      console.error('Failed to delete project:', e);
      alert(`删除项目失败: ${e instanceof Error ? e.message : '未知错误'}`);
    }
  };

  return (
    <div className={`${className}`}>
      {showHeader && (
        <header className="mb-6">
          <div className="flex items-center justify-between">
            <h1 className="text-2xl font-bold text-[var(--foreground)]">{t('title')}</h1>
            <Link href="/" className="text-sm text-[var(--accent-primary)] hover:underline">
              ← {t('backToHome')}
            </Link>
          </div>
        </header>
      )}

      {/* Search Bar and View Toggle */}
      <div className="mb-6 flex flex-col sm:flex-row gap-4">
        {/* Search Bar */}
        <div className="relative flex-1">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={t('searchPlaceholder')}
            className="input pl-4 pr-12"
          />
          {searchQuery && (
            <button
              onClick={clearSearch}
              className="absolute inset-y-0 right-0 flex items-center pr-3 text-[var(--muted)] hover:text-[var(--foreground)] transition-colors"
            >
              <FaTimes className="h-4 w-4" />
            </button>
          )}
        </div>

        {/* View Toggle */}
        <div className="flex items-center bg-[var(--background)] border border-[var(--border-color)] rounded-lg p-0.5">
          <button
            onClick={() => setViewMode('card')}
            className={`p-1.5 rounded-md transition-colors ${
              viewMode === 'card'
                ? 'bg-[var(--accent-primary)] text-white shadow-sm'
                : 'text-[var(--muted)] hover:text-[var(--foreground)]'
            }`}
            title="Card View"
          >
            <FaTh className="h-3.5 w-3.5" />
          </button>
          <button
            onClick={() => setViewMode('list')}
            className={`p-1.5 rounded-md transition-colors ${
              viewMode === 'list'
                ? 'bg-[var(--accent-primary)] text-white shadow-sm'
                : 'text-[var(--muted)] hover:text-[var(--foreground)]'
            }`}
            title="List View"
          >
            <FaList className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>

      {isLoading && <p className="text-[var(--muted)]">{t('loadingProjects')}</p>}
      {error && <p className="text-[var(--highlight)]">{t('errorLoading')} {error}</p>}

      {!isLoading && !error && filteredProjects.length > 0 && (
        <div className={viewMode === 'card' ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4' : 'space-y-2'}>
            {filteredProjects.map((project) => (
            viewMode === 'card' ? (
              <div key={project.id} className="card p-4 relative group">
                <button
                  type="button"
                  onClick={() => handleDelete(project)}
                  className="absolute top-2 right-2 text-[var(--muted)] hover:text-[var(--highlight)] opacity-0 group-hover:opacity-100 transition-opacity p-1"
                  title="Delete project"
                >
                  <FaTimes className="h-3.5 w-3.5" />
                </button>
                <Link
                  href={`/repositories/${project.repository_id || project.id}?type=${project.repo_type}&language=${project.language}`}
                  className="block"
                >
                  <h3 className="font-semibold text-[var(--foreground)] mb-2 line-clamp-2 group-hover:text-[var(--accent-primary)] transition-colors">
                    {project.display_name || project.name}
                  </h3>
                  <div className="flex flex-wrap gap-1.5 mb-3">
                    <span className="tag tag-primary">
                      {project.repo_type}
                    </span>
                    <span className="tag tag-default">
                      {project.language}
                    </span>
                  </div>
                  <p className="text-xs text-[var(--muted)]">
                    {t('processedOn')} {new Date(project.submittedAt).toLocaleDateString()}
                  </p>
                </Link>
              </div>
            ) : (
              <div key={project.id} className="card p-3 relative group flex items-center gap-4">
                <button
                  type="button"
                  onClick={() => handleDelete(project)}
                  className="text-[var(--muted)] hover:text-[var(--highlight)] opacity-0 group-hover:opacity-100 transition-opacity p-1 flex-shrink-0"
                  title="Delete project"
                >
                  <FaTimes className="h-3.5 w-3.5" />
                </button>
                <Link
                  href={`/repositories/${project.repository_id || project.id}?type=${project.repo_type}&language=${project.language}`}
                  className="flex-1 min-w-0 flex items-center justify-between gap-4"
                >
                  <div className="min-w-0">
                    <h3 className="font-medium text-[var(--foreground)] truncate group-hover:text-[var(--accent-primary)] transition-colors">
                      {project.display_name || project.name}
                    </h3>
                    <p className="text-xs text-[var(--muted)] mt-0.5">
                      {t('processedOn')} {new Date(project.submittedAt).toLocaleDateString()} · {project.repo_type} · {project.language}
                    </p>
                  </div>
                  <span className="tag tag-primary flex-shrink-0">
                    {project.repo_type}
                  </span>
                </Link>
              </div>
            )
          ))}
        </div>
      )}

      {!isLoading && !error && projects.length > 0 && filteredProjects.length === 0 && searchQuery && (
        <p className="text-[var(--muted)]">{t('noSearchResults')}</p>
      )}

      {!isLoading && !error && projects.length === 0 && (
        <p className="text-[var(--muted)]">{t('noProjects')}</p>
      )}
    </div>
  );
}
