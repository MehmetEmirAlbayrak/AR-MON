"""Unit tests for the MetricsCollector latency/depth aggregator (metrics.py)."""

import math

import pytest

from metrics import MetricsCollector


def _record_n(mc, totals, **common):
    """Record one sample per value in `totals`, filling other fields from common defaults."""
    for t in totals:
        mc.record(
            total_ms=t,
            yolo_ms=common.get("yolo_ms", t * 0.5),
            resnet_ms=common.get("resnet_ms", t * 0.1),
            depth_ms=common.get("depth_ms", None),
            n_objects=common.get("n_objects", 0),
            depth_method=common.get("depth_method", "heuristic"),
            biome=common.get("biome", "unknown"),
            depth_values=common.get("depth_values", None),
        )


class TestEmptyCollector:
    def test_empty_count_is_zero(self):
        mc = MetricsCollector()
        assert mc.summary()["count"] == 0

    def test_empty_summary_does_not_crash_and_zeroes(self):
        mc = MetricsCollector()
        s = mc.summary()
        assert s["throughput_fps"] == 0.0
        assert s["pct_under_target"] == 0.0
        assert s["latency_ms"]["total"]["mean"] == 0.0
        assert s["biomes"] == {}


class TestLatencyStats:
    def test_count_matches_records(self):
        mc = MetricsCollector()
        _record_n(mc, [10, 20, 30, 40, 50])
        assert mc.summary()["count"] == 5

    def test_mean_min_max(self):
        mc = MetricsCollector()
        _record_n(mc, [10, 20, 30, 40, 50])
        total = mc.summary()["latency_ms"]["total"]
        assert total["mean"] == 30.0
        assert total["min"] == 10.0
        assert total["max"] == 50.0

    def test_p50_is_median(self):
        mc = MetricsCollector()
        _record_n(mc, [10, 20, 30, 40, 50])
        assert mc.summary()["latency_ms"]["total"]["p50"] == 30.0

    def test_throughput_fps_is_inverse_of_mean(self):
        mc = MetricsCollector()
        _record_n(mc, [10, 20, 30, 40, 50])  # mean 30ms -> 33.33 fps
        # collector rounds to 2 decimals for clean report output
        assert math.isclose(mc.summary()["throughput_fps"], 1000.0 / 30.0, abs_tol=0.01)


class TestTargetThreshold:
    def test_all_under_target_is_100pct(self):
        mc = MetricsCollector(latency_target_ms=100.0)
        _record_n(mc, [10, 20, 30, 40, 50])
        assert mc.summary()["pct_under_target"] == 100.0

    def test_partial_under_target(self):
        mc = MetricsCollector(latency_target_ms=100.0)
        _record_n(mc, [10, 20, 30, 40, 50, 150])  # 5 of 6 under 100
        # collector rounds to 2 decimals for clean report output
        assert math.isclose(mc.summary()["pct_under_target"], 500.0 / 6.0, abs_tol=0.01)


class TestReset:
    def test_reset_clears_samples(self):
        mc = MetricsCollector()
        _record_n(mc, [10, 20, 30])
        mc.reset()
        assert mc.summary()["count"] == 0


class TestRollingWindow:
    def test_window_bounded_to_maxlen(self):
        mc = MetricsCollector(maxlen=3)
        _record_n(mc, [1, 2, 3, 4, 5])  # only last 3 (3,4,5) retained
        total = mc.summary()["latency_ms"]["total"]
        assert mc.summary()["count"] == 3
        assert total["min"] == 3.0
        assert total["max"] == 5.0


class TestDepthMetrics:
    def test_depth_latency_only_counts_midas_frames(self):
        mc = MetricsCollector()
        # 3 MiDaS frames (with depth_ms), 2 heuristic frames (depth_ms=None)
        mc.record(total_ms=80, yolo_ms=40, resnet_ms=10, depth_ms=100, depth_method="midas_async")
        mc.record(total_ms=80, yolo_ms=40, resnet_ms=10, depth_ms=110, depth_method="midas_async")
        mc.record(total_ms=80, yolo_ms=40, resnet_ms=10, depth_ms=120, depth_method="midas_async")
        mc.record(total_ms=80, yolo_ms=40, resnet_ms=10, depth_ms=None, depth_method="heuristic")
        mc.record(total_ms=80, yolo_ms=40, resnet_ms=10, depth_ms=None, depth_method="heuristic")
        s = mc.summary()
        assert s["latency_ms"]["depth"]["mean"] == 110.0
        assert s["depth"]["midas_frames"] == 3
        assert s["depth"]["heuristic_frames"] == 2
        assert math.isclose(s["depth"]["midas_coverage_pct"], 60.0, rel_tol=1e-6)

    def test_object_depth_value_distribution(self):
        mc = MetricsCollector()
        mc.record(total_ms=80, yolo_ms=40, resnet_ms=10, depth_values=[0.2, 0.4])
        mc.record(total_ms=80, yolo_ms=40, resnet_ms=10, depth_values=[0.6])
        od = mc.summary()["depth"]["object_depth"]
        assert od["count"] == 3
        assert math.isclose(od["mean"], (0.2 + 0.4 + 0.6) / 3.0, rel_tol=1e-6)
        assert od["min"] == 0.2
        assert od["max"] == 0.6


class TestBiomeDistribution:
    def test_biome_counts(self):
        mc = MetricsCollector()
        _record_n(mc, [10], biome="forest")
        _record_n(mc, [10], biome="forest")
        _record_n(mc, [10], biome="rocky")
        assert mc.summary()["biomes"] == {"forest": 2, "rocky": 1}
