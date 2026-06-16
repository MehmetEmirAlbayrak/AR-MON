"""
In-memory rolling metrics aggregator for the AR-MON server.

Records per-request latency (total, YOLO, ResNet, MiDaS depth) and depth
coverage/values over a bounded sliding window, then summarizes them on demand
(mean / p50 / p95 / p99 / min / max, throughput, % under the latency target,
depth coverage and value distribution, biome distribution).

Designed as a small, self-contained, thread-safe unit so the Flask server can
record one line per request and expose an aggregate via GET /metrics. Used to
produce measured latency/depth evidence (success-criteria reporting).
"""

import threading
from collections import Counter, deque
from typing import Any, Optional

import numpy as np


def _stats(values: list[float]) -> dict[str, float]:
    """
    Compute summary statistics for a list of numeric samples.

    Args:
        values: Sample values (may be empty).

    Returns:
        Dict with 'mean', 'p50', 'p95', 'p99', 'min', 'max', all rounded to 2
        decimals. Every field is 0.0 when `values` is empty.
    """
    if not values:
        return {"mean": 0.0, "p50": 0.0, "p95": 0.0, "p99": 0.0, "min": 0.0, "max": 0.0}
    arr = np.asarray(values, dtype=np.float64)
    return {
        "mean": round(float(arr.mean()), 2),
        "p50": round(float(np.percentile(arr, 50)), 2),
        "p95": round(float(np.percentile(arr, 95)), 2),
        "p99": round(float(np.percentile(arr, 99)), 2),
        "min": round(float(arr.min()), 2),
        "max": round(float(arr.max()), 2),
    }


class MetricsCollector:
    """
    Thread-safe rolling aggregator of per-request inference metrics.

    Keeps the most recent `maxlen` samples (older ones are dropped) so memory is
    bounded during long real-world test sessions. All public methods are safe to
    call from multiple Flask worker threads.
    """

    def __init__(self, maxlen: int = 2000, latency_target_ms: float = 100.0) -> None:
        """
        Args:
            maxlen: Maximum number of recent requests retained in the window.
            latency_target_ms: Success-criteria target for total request latency;
                `pct_under_target` reports the share of requests below it.
        """
        self._maxlen = maxlen
        self._target = float(latency_target_ms)
        self._lock = threading.Lock()
        self._samples: deque[dict[str, Any]] = deque(maxlen=maxlen)

    def record(
        self,
        *,
        total_ms: float,
        yolo_ms: float,
        resnet_ms: float,
        depth_ms: Optional[float] = None,
        n_objects: int = 0,
        depth_method: str = "heuristic",
        biome: str = "unknown",
        depth_values: Optional[list[float]] = None,
    ) -> None:
        """
        Record one request's metrics into the rolling window.

        Args:
            total_ms: Total server-side processing time for the request.
            yolo_ms: YOLO detector inference time.
            resnet_ms: ResNet scene-classifier inference time.
            depth_ms: Real MiDaS forward time for the latest depth map, or None
                when depth ran via the geometric heuristic / was unavailable.
            n_objects: Number of detected objects in this frame.
            depth_method: 'midas_async' (real depth model) or 'heuristic'.
            biome: Committed (smoothed) biome label for this frame.
            depth_values: Per-object depth values (0..1) attached this frame.
        """
        with self._lock:
            self._samples.append({
                "total_ms": float(total_ms),
                "yolo_ms": float(yolo_ms),
                "resnet_ms": float(resnet_ms),
                "depth_ms": None if depth_ms is None else float(depth_ms),
                "n_objects": int(n_objects),
                "depth_method": depth_method,
                "biome": biome,
                "depth_values": list(depth_values) if depth_values else [],
            })

    def reset(self) -> None:
        """Clear all recorded samples (start a fresh measurement session)."""
        with self._lock:
            self._samples.clear()

    def summary(self) -> dict[str, Any]:
        """
        Compute the aggregate report over the current window.

        Returns:
            A JSON-serializable dict with request count, latency target, percent
            of requests under the target, throughput (fps), per-component latency
            stats, depth coverage/values, biome distribution, and object counts.
            Safe (all-zero) shape when no samples have been recorded.
        """
        with self._lock:
            samples = list(self._samples)

        count = len(samples)
        total = [s["total_ms"] for s in samples]
        yolo = [s["yolo_ms"] for s in samples]
        resnet = [s["resnet_ms"] for s in samples]
        depth_lat = [s["depth_ms"] for s in samples if s["depth_ms"] is not None]

        mean_total = float(np.mean(total)) if total else 0.0
        under = sum(1 for t in total if t < self._target)
        pct_under = (under / count * 100.0) if count else 0.0
        fps = (1000.0 / mean_total) if mean_total > 0 else 0.0

        midas_frames = sum(1 for s in samples if s["depth_method"].startswith("midas"))
        heuristic_frames = count - midas_frames
        all_depth_values = [v for s in samples for v in s["depth_values"]]
        obj_total = sum(s["n_objects"] for s in samples)

        biomes = Counter(s["biome"] for s in samples)

        return {
            "count": count,
            "latency_target_ms": self._target,
            "pct_under_target": round(pct_under, 2),
            "throughput_fps": round(fps, 2),
            "latency_ms": {
                "total": _stats(total),
                "yolo": _stats(yolo),
                "resnet": _stats(resnet),
                "depth": _stats(depth_lat),
            },
            "depth": {
                "midas_frames": midas_frames,
                "heuristic_frames": heuristic_frames,
                "midas_coverage_pct": round(midas_frames / count * 100.0, 2) if count else 0.0,
                "object_depth": {
                    "count": len(all_depth_values),
                    "mean": round(float(np.mean(all_depth_values)), 4) if all_depth_values else 0.0,
                    "min": round(float(np.min(all_depth_values)), 4) if all_depth_values else 0.0,
                    "max": round(float(np.max(all_depth_values)), 4) if all_depth_values else 0.0,
                },
            },
            "biomes": dict(biomes),
            "objects": {
                "total": obj_total,
                "mean_per_frame": round(obj_total / count, 2) if count else 0.0,
            },
        }
