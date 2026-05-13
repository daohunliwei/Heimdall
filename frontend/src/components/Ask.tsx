'use client';

import React, {useState, useRef, useEffect} from 'react';
import {FaChevronLeft, FaChevronRight } from 'react-icons/fa';
import Markdown from './Markdown';
import { useLanguage } from '@/contexts/LanguageContext';
import RepoInfo from '@/types/repoinfo';
import getRepoUrl from '@/utils/getRepoUrl';
import ModelSelectionModal from './ModelSelectionModal';

interface Model {
  id: string;
  name: string;
}

interface Provider {
  id: string;
  name: string;
  models: Model[];
  supportsCustomModel?: boolean;
}

interface Message {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

interface AskTaskRequest {
  repo_url: string;
  owner?: string;
  repo?: string;
  question: string;
  history: Message[];
  deep_research: boolean;
  filePath?: string;
  token?: string;
  type?: string;
  provider?: string;
  model?: string;
  custom_model?: string;
  language?: string;
  excluded_dirs?: string;
  excluded_files?: string;
}

interface AskTaskResponse {
  content: string;
  stages: ResearchStage[];
  complete: boolean;
  iterations: number;
}

interface ResearchStage {
  title: string;
  content: string;
  iteration: number;
  type: 'plan' | 'update' | 'conclusion';
}

interface AskProps {
  repoInfo: RepoInfo;
  provider?: string;
  model?: string;
  isCustomModel?: boolean;
  customModel?: string;
  language?: string;
  onRef?: (ref: { clearConversation: () => void }) => void;
}

const Ask: React.FC<AskProps> = ({
  repoInfo,
  provider = '',
  model = '',
  isCustomModel = false,
  customModel = '',
  language = 'zh',
  onRef
}) => {
  const [question, setQuestion] = useState('');
  const [response, setResponse] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [deepResearch, setDeepResearch] = useState(false);

  // Model selection state
  const [selectedProvider, setSelectedProvider] = useState(provider);
  const [selectedModel, setSelectedModel] = useState(model);
  const [isCustomSelectedModel, setIsCustomSelectedModel] = useState(isCustomModel);
  const [customSelectedModel, setCustomSelectedModel] = useState(customModel);
  const [isModelSelectionModalOpen, setIsModelSelectionModalOpen] = useState(false);
  const [isComprehensiveView, setIsComprehensiveView] = useState(true);

  // Get language context for translations
  const { messages } = useLanguage();

  // Research navigation state
  const [researchStages, setResearchStages] = useState<ResearchStage[]>([]);
  const [currentStageIndex, setCurrentStageIndex] = useState(0);
  const [conversationHistory, setConversationHistory] = useState<Message[]>([]);
  const [researchIteration, setResearchIteration] = useState(0);
  const [researchComplete, setResearchComplete] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const responseRef = useRef<HTMLDivElement>(null);
  const providerRef = useRef(provider);
  const modelRef = useRef(model);

  // Focus input on component mount
  useEffect(() => {
    if (inputRef.current) {
      inputRef.current.focus();
    }
  }, []);

  // Expose clearConversation method to parent component
  useEffect(() => {
    if (onRef) {
      onRef({ clearConversation });
    }
  }, [onRef]);

  // Scroll to bottom of response when it changes
  useEffect(() => {
    if (responseRef.current) {
      responseRef.current.scrollTop = responseRef.current.scrollHeight;
    }
  }, [response]);

  useEffect(() => {
    providerRef.current = provider;
    modelRef.current = model;
  }, [provider, model]);

  useEffect(() => {
    const fetchModel = async () => {
      try {
        setIsLoading(true);

        const response = await fetch('/api/models/config');
        if (!response.ok) {
          throw new Error(`Error fetching model configurations: ${response.status}`);
        }

        const data = await response.json();

        // use latest provider/model ref to check
        if(providerRef.current == '' || modelRef.current== '') {
          setSelectedProvider(data.defaultProvider);

          // Find the default provider and set its default model
          const selectedProvider = data.providers.find((p:Provider) => p.id === data.defaultProvider);
          if (selectedProvider && selectedProvider.models.length > 0) {
            setSelectedModel(selectedProvider.models[0].id);
          }
        } else {
          setSelectedProvider(providerRef.current);
          setSelectedModel(modelRef.current);
        }
      } catch (err) {
        console.error('Failed to fetch model configurations:', err);
      } finally {
        setIsLoading(false);
      }
    };
    if(provider == '' || model == '') {
      fetchModel()
    }
  }, [provider, model]);

  const clearConversation = () => {
    setQuestion('');
    setResponse('');
    setConversationHistory([]);
    setResearchIteration(0);
    setResearchComplete(false);
    setResearchStages([]);
    setCurrentStageIndex(0);
    if (inputRef.current) {
      inputRef.current.focus();
    }
  };
  const downloadresponse = () =>{
  const blob = new Blob([response], { type: 'text/markdown' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `response-${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.md`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

  // Function to navigate to a specific research stage
  const navigateToStage = (index: number) => {
    if (index >= 0 && index < researchStages.length) {
      setCurrentStageIndex(index);
      setResponse(researchStages[index].content);
    }
  };

  // Function to navigate to the next research stage
  const navigateToNextStage = () => {
    if (currentStageIndex < researchStages.length - 1) {
      navigateToStage(currentStageIndex + 1);
    }
  };

  // Function to navigate to the previous research stage
  const navigateToPreviousStage = () => {
    if (currentStageIndex > 0) {
      navigateToStage(currentStageIndex - 1);
    }
  };

  const requestAskTask = async (requestBody: AskTaskRequest): Promise<AskTaskResponse> => {
    const taskResponse = await fetch('/api/tasks/ask', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(requestBody),
    });

    if (!taskResponse.ok) {
      const errorBody = await taskResponse.json().catch(() => ({ error: '请求失败' }));
      throw new Error(errorBody.error || `Ask 任务失败：${taskResponse.status}`);
    }

    return taskResponse.json();
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!question.trim() || isLoading) return;

    handleConfirmAsk();
  };

  // Handle confirm and send request
  const handleConfirmAsk = async () => {
    setIsLoading(true);
    setResponse('');
    setResearchStages([]);
    setCurrentStageIndex(0);
    setResearchIteration(0);
    setResearchComplete(false);

    try {
      const initialMessage: Message = {
        role: 'user',
        content: question
      };

      const requestBody: AskTaskRequest = {
        repo_url: getRepoUrl(repoInfo),
        owner: repoInfo.owner,
        repo: repoInfo.repo,
        type: repoInfo.type,
        question: question,
        history: conversationHistory.map(msg => ({ role: msg.role as 'user' | 'assistant' | 'system', content: msg.content })),
        deep_research: deepResearch,
        provider: selectedProvider,
        model: isCustomSelectedModel ? undefined : selectedModel,
        custom_model: isCustomSelectedModel ? customSelectedModel : undefined,
        language: language
      };

      if (repoInfo?.token) {
        requestBody.token = repoInfo.token;
      }

      const result = await requestAskTask(requestBody);
      setResponse(result.content);
      setResearchStages(result.stages || []);
      setCurrentStageIndex(result.stages && result.stages.length > 0 ? result.stages.length - 1 : 0);
      setResearchIteration(result.iterations || 1);
      setResearchComplete(result.complete);
      setConversationHistory([
        ...conversationHistory,
        initialMessage,
        { role: 'assistant', content: result.content }
      ]);
    } catch (error) {
      console.error('Error during API call:', error);
      setResponse('获取回答失败，请稍后重试。');
      setResearchComplete(true);
    } finally {
      setIsLoading(false);
    }
  };

  const [buttonWidth, setButtonWidth] = useState(0);
  const buttonRef = useRef<HTMLButtonElement>(null);

  // Measure button width and update state
  useEffect(() => {
    if (buttonRef.current) {
      const width = buttonRef.current.offsetWidth;
      setButtonWidth(width);
    }
  }, [messages.ask?.askButton, isLoading]);

  return (
    <div>
      <div className="p-4">
        <div className="flex items-center justify-end mb-4">
          {/* Model selection button */}
          <button
            type="button"
            onClick={() => setIsModelSelectionModalOpen(true)}
            className="tag tag-default cursor-pointer hover:border-[var(--accent-primary)]/30 transition-colors flex items-center gap-1.5"
          >
            <span>{selectedProvider}/{isCustomSelectedModel ? customSelectedModel : selectedModel}</span>
            <svg className="h-3.5 w-3.5 text-[var(--accent-primary)]/70" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </button>
        </div>

        {/* Question input */}
        <form onSubmit={handleSubmit} className="mt-4">
          <div className="relative">
            <input
              ref={inputRef}
              type="text"
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              placeholder={messages.ask?.placeholder || 'What would you like to know about this codebase?'}
              className="input py-3 text-sm"
              style={{ paddingRight: `${buttonWidth + 24}px` }}
              disabled={isLoading}
            />
            <button
              ref={buttonRef}
              type="submit"
              disabled={isLoading || !question.trim()}
              className={`absolute right-3 top-1/2 transform -translate-y-1/2 px-4 py-1.5 rounded-md font-medium text-sm ${
                isLoading || !question.trim()
                  ? 'bg-[var(--border-color)] text-[var(--muted)] cursor-not-allowed'
                  : 'bg-[var(--accent-primary)] text-white hover:bg-[var(--accent-primary-hover)] shadow-sm'
              } transition-all duration-200 flex items-center gap-1.5`}
            >
              {isLoading ? (
                <div className="w-4 h-4 rounded-full border-2 border-t-transparent border-white animate-spin" />
              ) : (
                <>
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 5l7 7-7 7M5 5l7 7-7 7" />
                  </svg>
                  <span>{messages.ask?.askButton || 'Ask'}</span>
                </>
              )}
            </button>
          </div>

          {/* Deep Research toggle */}
          <div className="flex items-center mt-2 justify-between">
            <div className="group relative">
              <label className="flex items-center cursor-pointer">
                <span className="text-xs text-[var(--muted)] mr-2">Deep Research</span>
                <div className="relative">
                  <input
                    type="checkbox"
                    checked={deepResearch}
                    onChange={() => setDeepResearch(!deepResearch)}
                    className="sr-only"
                  />
                  <div className={`w-10 h-5 rounded-full transition-colors ${deepResearch ? 'bg-[var(--accent-primary)]' : 'bg-[var(--border-color)]'}`}></div>
                  <div className={`absolute left-0.5 top-0.5 w-4 h-4 rounded-full bg-white transition-transform transform ${deepResearch ? 'translate-x-5' : ''}`}></div>
                </div>
              </label>
              <div className="absolute bottom-full left-0 mb-2 hidden group-hover:block bg-gray-800 text-white text-xs rounded p-2 w-72 z-10">
                <div className="relative">
                  <div className="absolute -bottom-2 left-4 w-0 h-0 border-l-4 border-r-4 border-t-4 border-transparent border-t-gray-800"></div>
                  <p className="mb-1">深度研究会调用后端多轮任务流程：</p>
                  <ul className="list-disc pl-4 text-xs">
                    <li><strong>研究计划：</strong>生成初始分析与问题拆解</li>
                    <li><strong>阶段推进：</strong>按后端返回的阶段逐步补充结果</li>
                    <li><strong>最终结论：</strong>汇总多轮研究后的完整回答</li>
                  </ul>
                  <p className="mt-1 text-xs italic">具体轮次与完成状态由后端任务接口返回</p>
                </div>
              </div>
            </div>
            {deepResearch && (
              <div className="text-xs text-[var(--accent-primary)]">
                已开启深度研究
                {researchIteration > 0 && !researchComplete && `（第 ${researchIteration} 轮）`}
                {researchComplete && '（已完成）'}
              </div>
            )}
          </div>
        </form>

        {/* Response area */}
        {response && (
          <div className="border-t border-[var(--border-color)] mt-4">
            <div
              ref={responseRef}
              className="p-4 max-h-[400px] overflow-y-auto"
            >
              <Markdown content={response} />
            </div>

            <div className="p-2 flex justify-between items-center border-t border-[var(--border-color)]">
              {deepResearch && researchStages.length > 1 && (
                <div className="flex items-center space-x-2">
                  <button onClick={() => navigateToPreviousStage()} disabled={currentStageIndex === 0}
                    className={`p-1 rounded-md ${currentStageIndex === 0 ? 'text-[var(--muted-light)]' : 'text-[var(--muted)] hover:bg-[var(--background)]'}`}
                    aria-label="Previous stage">
                    <FaChevronLeft size={12} />
                  </button>
                  <div className="text-xs text-[var(--muted)]">
                    {currentStageIndex + 1} / {researchStages.length}
                  </div>
                  <button onClick={() => navigateToNextStage()} disabled={currentStageIndex === researchStages.length - 1}
                    className={`p-1 rounded-md ${currentStageIndex === researchStages.length - 1 ? 'text-[var(--muted-light)]' : 'text-[var(--muted)] hover:bg-[var(--background)]'}`}
                    aria-label="Next stage">
                    <FaChevronRight size={12} />
                  </button>
                  <div className="text-xs text-[var(--muted)] ml-2">
                    {researchStages[currentStageIndex]?.title || `Stage ${currentStageIndex + 1}`}
                  </div>
                </div>
              )}

            <div className="flex items-center space-x-2">
              <button onClick={downloadresponse}
                className="btn-ghost text-xs flex items-center gap-1 hover:text-[var(--success)]"
                title="Download response as markdown file">
                <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                </svg>
                Download
              </button>
              <button id="ask-clear-conversation" onClick={clearConversation}
                className="btn-ghost text-xs hover:text-[var(--highlight)]">
                Clear conversation
              </button>
            </div>
              </div>
          </div>
        )}

        {isLoading && !response && (
          <div className="p-4 border-t border-[var(--border-color)]">
            <div className="flex items-center space-x-2">
              <div className="animate-pulse flex space-x-1">
                <div className="h-2 w-2 bg-[var(--accent-primary)] rounded-full"></div>
                <div className="h-2 w-2 bg-[var(--accent-primary)] rounded-full"></div>
                <div className="h-2 w-2 bg-[var(--accent-primary)] rounded-full"></div>
              </div>
              <span className="text-xs text-[var(--muted)]">
                {deepResearch
                  ? (researchIteration === 0 ? '正在启动深度研究任务...' : `后端正在执行第 ${researchIteration} 轮研究...`)
                  : '正在生成回答...'}
              </span>
            </div>
          </div>
        )}
      </div>

      {/* Model Selection Modal */}
      <ModelSelectionModal
        isOpen={isModelSelectionModalOpen}
        onClose={() => setIsModelSelectionModalOpen(false)}
        provider={selectedProvider}
        setProvider={setSelectedProvider}
        model={selectedModel}
        setModel={setSelectedModel}
        isCustomModel={isCustomSelectedModel}
        setIsCustomModel={setIsCustomSelectedModel}
        customModel={customSelectedModel}
        setCustomModel={setCustomSelectedModel}
        isComprehensiveView={isComprehensiveView}
        setIsComprehensiveView={setIsComprehensiveView}
        showFileFilters={false}
        onApply={() => {
          console.log('Model selection applied:', selectedProvider, selectedModel);
        }}
        showWikiType={false}
        authRequired={false}
        isAuthLoading={false}
      />
    </div>
  );
};

export default Ask;
