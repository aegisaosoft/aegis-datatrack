-- Vehicle Tracking - Additional Functions and Maintenance
-- Run after 001_tracking_schema.sql

-- ============================================================================
-- PARTITION MANAGEMENT (for large datasets)
-- ============================================================================

-- Create partitioned table for vehicle_locations if needed
-- This is useful when you have millions of records
-- Uncomment and adapt as needed for your scale

/*
-- Create partitioned table
CREATE TABLE public.vehicle_locations_partitioned (
    LIKE public.vehicle_locations INCLUDING ALL
) PARTITION BY RANGE (device_timestamp);

-- Create monthly partitions for the next year
DO $$
DECLARE
    start_date DATE := DATE_TRUNC('month', CURRENT_DATE);
    partition_date DATE;
    partition_name TEXT;
BEGIN
    FOR i IN 0..12 LOOP
        partition_date := start_date + (i || ' months')::INTERVAL;
        partition_name := 'vehicle_locations_' || TO_CHAR(partition_date, 'YYYY_MM');
        
        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS public.%I PARTITION OF public.vehicle_locations_partitioned
             FOR VALUES FROM (%L) TO (%L)',
            partition_name,
            partition_date,
            partition_date + INTERVAL '1 month'
        );
    END LOOP;
END $$;
*/

-- ============================================================================
-- AGGREGATE STATISTICS FUNCTIONS
-- ============================================================================

-- Get daily statistics for a vehicle
CREATE OR REPLACE FUNCTION get_vehicle_daily_stats(
    p_vehicle_id uuid,
    p_date date DEFAULT CURRENT_DATE
)
RETURNS TABLE (
    total_distance_meters bigint,
    total_trips int,
    total_drive_time_minutes int,
    total_idle_time_minutes int,
    max_speed_kmh int,
    first_move_time timestamp,
    last_stop_time timestamp
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        COALESCE(SUM(vt.distance_meters)::bigint, 0) as total_distance_meters,
        COUNT(vt.id)::int as total_trips,
        COALESCE(SUM(EXTRACT(EPOCH FROM (vt.end_time - vt.start_time)) / 60)::int, 0) as total_drive_time_minutes,
        COALESCE(SUM(vt.idle_duration_seconds / 60)::int, 0) as total_idle_time_minutes,
        COALESCE(MAX(vt.max_speed_kmh)::int, 0) as max_speed_kmh,
        MIN(vt.start_time) as first_move_time,
        MAX(vt.end_time) as last_stop_time
    FROM public.vehicle_trips vt
    WHERE vt.vehicle_id = p_vehicle_id
      AND vt.start_time >= p_date
      AND vt.start_time < p_date + INTERVAL '1 day'
      AND vt.status = 'completed';
END;
$$ LANGUAGE plpgsql;

-- Get weekly statistics for a vehicle
CREATE OR REPLACE FUNCTION get_vehicle_weekly_stats(
    p_vehicle_id uuid,
    p_week_start date DEFAULT DATE_TRUNC('week', CURRENT_DATE)::date
)
RETURNS TABLE (
    day_of_week int,
    day_date date,
    total_distance_meters bigint,
    total_trips int,
    avg_speed_kmh int
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        EXTRACT(DOW FROM vt.start_time)::int as day_of_week,
        vt.start_time::date as day_date,
        COALESCE(SUM(vt.distance_meters)::bigint, 0) as total_distance_meters,
        COUNT(vt.id)::int as total_trips,
        COALESCE(AVG(vt.avg_speed_kmh)::int, 0) as avg_speed_kmh
    FROM public.vehicle_trips vt
    WHERE vt.vehicle_id = p_vehicle_id
      AND vt.start_time >= p_week_start
      AND vt.start_time < p_week_start + INTERVAL '7 days'
      AND vt.status = 'completed'
    GROUP BY EXTRACT(DOW FROM vt.start_time), vt.start_time::date
    ORDER BY vt.start_time::date;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- GEOFENCE FUNCTIONS
-- ============================================================================

-- Check if a point is inside a circular geofence
CREATE OR REPLACE FUNCTION is_point_in_circle(
    p_lat decimal(10, 7),
    p_lng decimal(10, 7),
    p_center_lat decimal(10, 7),
    p_center_lng decimal(10, 7),
    p_radius_meters int
)
RETURNS boolean AS $$
DECLARE
    distance_meters float;
BEGIN
    -- Haversine formula
    distance_meters := 6371000 * 2 * ASIN(
        SQRT(
            POWER(SIN(RADIANS(p_lat - p_center_lat) / 2), 2) +
            COS(RADIANS(p_center_lat)) * COS(RADIANS(p_lat)) *
            POWER(SIN(RADIANS(p_lng - p_center_lng) / 2), 2)
        )
    );
    
    RETURN distance_meters <= p_radius_meters;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- Calculate distance between two points in meters
CREATE OR REPLACE FUNCTION calculate_distance_meters(
    p_lat1 decimal(10, 7),
    p_lng1 decimal(10, 7),
    p_lat2 decimal(10, 7),
    p_lng2 decimal(10, 7)
)
RETURNS float AS $$
BEGIN
    RETURN 6371000 * 2 * ASIN(
        SQRT(
            POWER(SIN(RADIANS(p_lat2 - p_lat1) / 2), 2) +
            COS(RADIANS(p_lat1)) * COS(RADIANS(p_lat2)) *
            POWER(SIN(RADIANS(p_lng2 - p_lng1) / 2), 2)
        )
    );
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- ============================================================================
-- MAINTENANCE JOBS
-- ============================================================================

-- Archive old location data (move to archive table or delete)
CREATE OR REPLACE FUNCTION archive_old_locations(
    p_days_to_keep int DEFAULT 90,
    p_batch_size int DEFAULT 10000
)
RETURNS TABLE (
    archived_count int,
    remaining_count bigint
) AS $$
DECLARE
    cutoff_date timestamp;
    deleted int := 0;
    total_remaining bigint;
BEGIN
    cutoff_date := CURRENT_TIMESTAMP - (p_days_to_keep || ' days')::interval;
    
    -- Delete in batches to avoid long locks
    LOOP
        DELETE FROM public.vehicle_locations
        WHERE id IN (
            SELECT id FROM public.vehicle_locations
            WHERE device_timestamp < cutoff_date
            LIMIT p_batch_size
        );
        
        GET DIAGNOSTICS deleted = ROW_COUNT;
        EXIT WHEN deleted = 0;
        
        COMMIT;
    END LOOP;
    
    SELECT COUNT(*) INTO total_remaining FROM public.vehicle_locations;
    
    RETURN QUERY SELECT deleted, total_remaining;
END;
$$ LANGUAGE plpgsql;

-- Vacuum and analyze tracking tables
CREATE OR REPLACE FUNCTION maintain_tracking_tables()
RETURNS void AS $$
BEGIN
    VACUUM ANALYZE public.vehicle_locations;
    VACUUM ANALYZE public.vehicle_tracking_status;
    VACUUM ANALYZE public.vehicle_trips;
    VACUUM ANALYZE public.vehicle_events;
    VACUUM ANALYZE public.tracking_sync_log;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- REPORTING VIEWS
-- ============================================================================

-- Vehicle utilization report
CREATE OR REPLACE VIEW public.v_vehicle_utilization AS
WITH daily_stats AS (
    SELECT 
        vt.vehicle_id,
        vt.start_time::date as trip_date,
        COUNT(*) as trip_count,
        SUM(vt.distance_meters) as total_distance,
        SUM(EXTRACT(EPOCH FROM (COALESCE(vt.end_time, CURRENT_TIMESTAMP) - vt.start_time))) as total_drive_seconds
    FROM public.vehicle_trips vt
    WHERE vt.start_time >= CURRENT_DATE - INTERVAL '30 days'
    GROUP BY vt.vehicle_id, vt.start_time::date
)
SELECT 
    v.id as vehicle_id,
    v.license_plate,
    COUNT(DISTINCT ds.trip_date) as active_days,
    COALESCE(SUM(ds.trip_count), 0) as total_trips,
    COALESCE(SUM(ds.total_distance) / 1609.34, 0)::decimal(10,1) as total_miles,
    COALESCE(SUM(ds.total_drive_seconds) / 3600, 0)::decimal(10,1) as total_drive_hours,
    CASE 
        WHEN COUNT(DISTINCT ds.trip_date) > 0 
        THEN (SUM(ds.total_distance) / 1609.34 / COUNT(DISTINCT ds.trip_date))::decimal(10,1)
        ELSE 0 
    END as avg_daily_miles
FROM public.vehicles v
LEFT JOIN daily_stats ds ON ds.vehicle_id = v.id
GROUP BY v.id, v.license_plate;

-- Event summary for alerting
CREATE OR REPLACE VIEW public.v_event_summary AS
SELECT 
    ve.vehicle_id,
    v.license_plate,
    ve.event_type,
    ve.severity,
    COUNT(*) as event_count,
    MAX(ve.event_time) as last_occurrence,
    COUNT(*) FILTER (WHERE ve.acknowledged_at IS NULL) as unacknowledged_count
FROM public.vehicle_events ve
JOIN public.vehicles v ON v.id = ve.vehicle_id
WHERE ve.event_time >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY ve.vehicle_id, v.license_plate, ve.event_type, ve.severity
ORDER BY ve.vehicle_id, ve.event_type;

-- ============================================================================
-- SCHEDULED JOB SETUP (requires pg_cron extension)
-- ============================================================================

-- Uncomment if you have pg_cron installed:
/*
-- Schedule daily cleanup of old locations
SELECT cron.schedule('cleanup-old-locations', '0 3 * * *', 
    $$SELECT cleanup_old_locations(90)$$);

-- Schedule daily maintenance
SELECT cron.schedule('maintain-tracking-tables', '0 4 * * *', 
    $$SELECT maintain_tracking_tables()$$);

-- Schedule cleanup of old sync logs
SELECT cron.schedule('cleanup-sync-logs', '0 5 * * 0', 
    $$DELETE FROM tracking_sync_log WHERE started_at < CURRENT_TIMESTAMP - INTERVAL '30 days'$$);
*/

-- ============================================================================
-- INDEXES FOR COMMON QUERIES
-- ============================================================================

-- Index for finding vehicles that haven't reported recently
CREATE INDEX IF NOT EXISTS idx_tracking_status_stale 
    ON public.vehicle_tracking_status (device_timestamp) 
    WHERE device_timestamp < CURRENT_TIMESTAMP - INTERVAL '1 hour';

-- Index for speeding events
CREATE INDEX IF NOT EXISTS idx_vehicle_events_speeding 
    ON public.vehicle_events (vehicle_id, event_time) 
    WHERE event_type = 'speeding';

-- Index for starter events
CREATE INDEX IF NOT EXISTS idx_vehicle_events_starter 
    ON public.vehicle_events (vehicle_id, event_time) 
    WHERE event_type IN ('starter_disabled', 'starter_enabled');
