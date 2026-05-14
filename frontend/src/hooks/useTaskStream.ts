"use client";

import { useEffect, useState, useRef, useCallback } from "react";

export interface TaskProgress {
  phase: string;
  percent: number;
  message: string;
}

export interface TaskComplete {
  task_id: string;
  result: Record<string, unknown>;
}

export function useTaskStream(taskId: string | null) {
  const [progress, setProgress] = useState<TaskProgress | null>(null);
  const [isComplete, setIsComplete] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<TaskComplete | null>(null);
  const eventSourceRef = useRef<EventSource | null>(null);

  const connect = useCallback(() => {
    if (!taskId) return;
    if (eventSourceRef.current) eventSourceRef.current.close();

    const es = new EventSource(`${process.env.NEXT_PUBLIC_API_URL || ""}/tasks/${taskId}/stream`);
    eventSourceRef.current = es;

    es.addEventListener("progress", (e) => {
      const data = JSON.parse(e.data) as TaskProgress;
      setProgress(data);
    });

    es.addEventListener("complete", (e) => {
      const data = JSON.parse(e.data) as TaskComplete;
      setResult(data);
      setIsComplete(true);
      es.close();
    });

    es.addEventListener("error", (e) => {
      try {
        const data = JSON.parse((e as MessageEvent).data || "{}");
        setError(data.message || "任务执行失败");
      } catch {
        setError("连接中断");
      }
      setIsComplete(true);
      es.close();
    });

    es.onerror = () => {
      // SSE 连接错误，会自动重连
    };
  }, [taskId]);

  useEffect(() => {
    connect();
    return () => {
      if (eventSourceRef.current) eventSourceRef.current.close();
    };
  }, [connect]);

  const reset = useCallback(() => {
    setProgress(null);
    setIsComplete(false);
    setError(null);
    setResult(null);
    eventSourceRef.current?.close();
    connect();
  }, [connect]);

  return { progress, isComplete, error, result, reset };
}
