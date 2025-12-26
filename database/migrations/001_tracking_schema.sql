-- Vehicle Tracking Schema for aegis_ao_rental
-- Adds GPS tracking tables to existing database
-- Safe to run multiple times (uses IF NOT EXISTS / OR REPLACE)

-- ============================================================================
-- TRACKING DEVICES TABLE
-- Maps Datatrack GPS devices to vehicles
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.tracking_devices (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    vehicle_id uuid NOT NULL,
    serial varchar(50) NOT NULL,
    device_name varchar(100) NULL,
    imei varchar(20) NULL,
    sim_number varchar(20) NULL,
    firmware_version varchar(20) NULL,
    is_active boolean DEFAULT true NOT NULL,
    installed_at timestamp NULL,
    last_communication_at timestamp NULL,
    created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    CONSTRAINT tracking_devices_pkey PRIMARY KEY (id)
);

-- Add constraints if not exist (safe for re-run)
DO $$ BEGIN
    ALTER TABLE public.tracking_devices ADD CONSTRAINT tracking_devices_serial_key UNIQUE (serial);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE public.tracking_devices ADD CONSTRAINT tracking_devices_vehicle_key UNIQUE (vehicle_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE public.tracking_devices ADD CONSTRAINT fk_tracking_devices_vehicle 
        FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS idx_tracking_devices_vehicle ON public.tracking_devices (vehicle_id);
CREATE INDEX IF NOT EXISTS idx_tracking_devices_serial ON public.tracking_devices (serial);

-- ============================================================================
-- VEHICLE LOCATIONS TABLE
-- Historical location data from GPS tracking devices
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.vehicle_locations (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    vehicle_id uuid NOT NULL,
    device_serial varchar(50) NOT NULL,
    latitude decimal(10, 7) NOT NULL,
    longitude decimal(10, 7) NOT NULL,
    altitude decimal(8, 2) NULL,
    heading smallint NULL,
    speed_kmh smallint DEFAULT 0 NULL,
    odometer_meters bigint NULL,
    location_type_id smallint NOT NULL,
    gps_quality smallint DEFAULT 0 NULL,
    voltage_mv int NULL,
    ignition_on boolean DEFAULT false NULL,
    starter_disabled boolean DEFAULT false NULL,
    device_timestamp timestamp NOT NULL,
    received_at timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT vehicle_locations_pkey PRIMARY KEY (id)
);

DO $$ BEGIN
    ALTER TABLE public.vehicle_locations ADD CONSTRAINT fk_vehicle_locations_vehicle 
        FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS idx_vehicle_locations_vehicle_time ON public.vehicle_locations (vehicle_id, device_timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_vehicle_locations_device_time ON public.vehicle_locations (device_serial, device_timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_vehicle_locations_timestamp ON public.vehicle_locations (device_timestamp DESC);

-- ============================================================================
-- VEHICLE TRACKING STATUS TABLE
-- Current status for each vehicle (latest position)
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.vehicle_tracking_status (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    vehicle_id uuid NOT NULL,
    device_serial varchar(50) NOT NULL,
    latitude decimal(10, 7) NOT NULL,
    longitude decimal(10, 7) NOT NULL,
    address varchar(500) NULL,
    speed_kmh smallint DEFAULT 0 NULL,
    heading smallint NULL,
    location_type_id smallint NOT NULL,
    voltage_mv int NULL,
    odometer_meters bigint NULL,
    is_moving boolean DEFAULT false NULL,
    ignition_on boolean DEFAULT false NULL,
    starter_disabled boolean DEFAULT false NULL,
    device_timestamp timestamp NOT NULL,
    last_updated timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT vehicle_tracking_status_pkey PRIMARY KEY (id)
);

DO $$ BEGIN
    ALTER TABLE public.vehicle_tracking_status ADD CONSTRAINT vehicle_tracking_status_vehicle_key UNIQUE (vehicle_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE public.vehicle_tracking_status ADD CONSTRAINT fk_vehicle_tracking_status_vehicle 
        FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

-- ============================================================================
-- VEHICLE TRIPS TABLE
-- Individual trips from ignition on to ignition off
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.vehicle_trips (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    vehicle_id uuid NOT NULL,
    device_serial varchar(50) NOT NULL,
    start_time timestamp NOT NULL,
    end_time timestamp NULL,
    start_latitude decimal(10, 7) NOT NULL,
    start_longitude decimal(10, 7) NOT NULL,
    start_address varchar(500) NULL,
    end_latitude decimal(10, 7) NULL,
    end_longitude decimal(10, 7) NULL,
    end_address varchar(500) NULL,
    distance_meters int DEFAULT 0 NULL,
    max_speed_kmh smallint DEFAULT 0 NULL,
    avg_speed_kmh smallint DEFAULT 0 NULL,
    idle_duration_seconds int DEFAULT 0 NULL,
    start_odometer_meters bigint NULL,
    end_odometer_meters bigint NULL,
    status varchar(20) DEFAULT 'in_progress' NULL,
    created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    CONSTRAINT vehicle_trips_pkey PRIMARY KEY (id)
);

DO $$ BEGIN
    ALTER TABLE public.vehicle_trips ADD CONSTRAINT fk_vehicle_trips_vehicle 
        FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS idx_vehicle_trips_vehicle_time ON public.vehicle_trips (vehicle_id, start_time DESC);
CREATE INDEX IF NOT EXISTS idx_vehicle_trips_status ON public.vehicle_trips (status);

-- ============================================================================
-- VEHICLE EVENTS TABLE
-- Significant vehicle events (ignition, alerts, etc.)
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.vehicle_events (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    vehicle_id uuid NOT NULL,
    device_serial varchar(50) NOT NULL,
    event_type varchar(50) NOT NULL,
    event_code smallint NULL,
    severity varchar(20) DEFAULT 'info' NULL,
    latitude decimal(10, 7) NULL,
    longitude decimal(10, 7) NULL,
    address varchar(500) NULL,
    event_data jsonb NULL,
    event_time timestamp NOT NULL,
    received_at timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
    acknowledged_at timestamp NULL,
    acknowledged_by uuid NULL,
    CONSTRAINT vehicle_events_pkey PRIMARY KEY (id)
);

DO $$ BEGIN
    ALTER TABLE public.vehicle_events ADD CONSTRAINT fk_vehicle_events_vehicle 
        FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS idx_vehicle_events_vehicle_time ON public.vehicle_events (vehicle_id, event_time DESC);
CREATE INDEX IF NOT EXISTS idx_vehicle_events_type ON public.vehicle_events (event_type);
CREATE INDEX IF NOT EXISTS idx_vehicle_events_time ON public.vehicle_events (event_time DESC);

-- ============================================================================
-- TRACKING SYNC LOG TABLE
-- Log of data synchronization with Datatrack API
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.tracking_sync_log (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    sync_type varchar(50) NOT NULL,
    started_at timestamp NOT NULL,
    completed_at timestamp NULL,
    records_fetched int DEFAULT 0 NULL,
    records_inserted int DEFAULT 0 NULL,
    records_updated int DEFAULT 0 NULL,
    status varchar(20) DEFAULT 'running' NULL,
    error_message text NULL,
    CONSTRAINT tracking_sync_log_pkey PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS idx_tracking_sync_log_type_time ON public.tracking_sync_log (sync_type, started_at DESC);

-- ============================================================================
-- LOCATION TYPES REFERENCE TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.location_types (
    id smallint NOT NULL,
    name varchar(50) NOT NULL,
    description varchar(200) NULL,
    CONSTRAINT location_types_pkey PRIMARY KEY (id)
);

INSERT INTO public.location_types (id, name, description) VALUES
    (2, 'ignition_off', 'Ignition turned off'),
    (3, 'stopped', 'Vehicle stopped with ignition on'),
    (4, 'ignition_on', 'Ignition turned on'),
    (5, 'moving', 'Vehicle in motion'),
    (24, 'starter_disabled', 'Starter was disabled'),
    (25, 'starter_enabled', 'Starter was enabled'),
    (26, 'stopped_idle', 'Stopped and idle')
ON CONFLICT (id) DO NOTHING;

-- ============================================================================
-- TRIGGERS
-- ============================================================================
CREATE OR REPLACE FUNCTION update_tracking_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

DROP TRIGGER IF EXISTS update_tracking_devices_updated_at ON public.tracking_devices;
CREATE TRIGGER update_tracking_devices_updated_at 
    BEFORE UPDATE ON public.tracking_devices 
    FOR EACH ROW EXECUTE FUNCTION update_tracking_updated_at();

DROP TRIGGER IF EXISTS update_vehicle_trips_updated_at ON public.vehicle_trips;
CREATE TRIGGER update_vehicle_trips_updated_at 
    BEFORE UPDATE ON public.vehicle_trips 
    FOR EACH ROW EXECUTE FUNCTION update_tracking_updated_at();

-- ============================================================================
-- VIEW: Vehicle tracking summary
-- ============================================================================
CREATE OR REPLACE VIEW public.v_vehicle_tracking_summary AS
SELECT 
    v.id AS vehicle_id,
    v.license_plate,
    v.vin,
    v.color AS vehicle_color,
    v.status AS rental_status,
    c.company_name,
    td.serial AS device_serial,
    td.device_name,
    td.is_active AS device_active,
    vts.latitude,
    vts.longitude,
    vts.address,
    vts.speed_kmh,
    vts.is_moving,
    vts.ignition_on,
    vts.starter_disabled,
    vts.voltage_mv,
    vts.device_timestamp AS last_location_time,
    vts.last_updated,
    EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - vts.device_timestamp)) / 60 AS minutes_since_update
FROM public.vehicles v
LEFT JOIN public.companies c ON c.id = v.company_id
LEFT JOIN public.tracking_devices td ON td.vehicle_id = v.id AND td.is_active = true
LEFT JOIN public.vehicle_tracking_status vts ON vts.vehicle_id = v.id;

-- ============================================================================
-- CLEANUP FUNCTION
-- ============================================================================
CREATE OR REPLACE FUNCTION cleanup_old_locations(p_days_to_keep int DEFAULT 90)
RETURNS int AS $$
DECLARE
    deleted_count int;
BEGIN
    DELETE FROM public.vehicle_locations
    WHERE device_timestamp < CURRENT_TIMESTAMP - (p_days_to_keep || ' days')::interval;
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Done!
SELECT 'Tracking schema applied successfully' AS status;
