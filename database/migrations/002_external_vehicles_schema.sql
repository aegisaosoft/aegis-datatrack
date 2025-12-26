-- =============================================
-- External Vehicle Integration Schema
-- Links our vehicles to external tracking systems
-- =============================================

-- External companies (tracking providers like Datatrack 247)
CREATE TABLE IF NOT EXISTS external_companies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_name VARCHAR(255) NOT NULL,
    api_base_url VARCHAR(500),
    api_key_name VARCHAR(100),  -- Name of the config key for API credentials
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Unique constraint on company name
DO $$ BEGIN
    ALTER TABLE external_companies ADD CONSTRAINT uq_external_companies_name UNIQUE (company_name);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Vehicles as they exist in external systems
CREATE TABLE IF NOT EXISTS external_company_vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_company_id UUID NOT NULL REFERENCES external_companies(id) ON DELETE CASCADE,
    external_id VARCHAR(100) NOT NULL,  -- ID in external system (serial, device_id, etc.)
    name VARCHAR(255),
    vin VARCHAR(17),
    license_plate VARCHAR(50),
    make VARCHAR(100),
    model VARCHAR(100),
    year INT,
    color VARCHAR(50),
    notes TEXT,
    raw_data JSONB,  -- Full response from external API
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_synced_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Unique external_id per company
DO $$ BEGIN
    ALTER TABLE external_company_vehicles 
    ADD CONSTRAINT uq_external_company_vehicles_ext_id 
    UNIQUE (external_company_id, external_id);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Index for lookups
CREATE INDEX IF NOT EXISTS idx_external_company_vehicles_company 
ON external_company_vehicles(external_company_id);

CREATE INDEX IF NOT EXISTS idx_external_company_vehicles_external_id 
ON external_company_vehicles(external_id);

-- Links our vehicles to external company vehicles (many-to-many)
CREATE TABLE IF NOT EXISTS external_vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vehicle_id UUID NOT NULL REFERENCES vehicles(id) ON DELETE CASCADE,
    external_company_vehicle_id UUID NOT NULL REFERENCES external_company_vehicles(id) ON DELETE CASCADE,
    is_primary BOOLEAN NOT NULL DEFAULT true,  -- Primary tracker for this vehicle
    linked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    linked_by UUID,  -- User who created the link
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- One external vehicle can only be linked to one of our vehicles
DO $$ BEGIN
    ALTER TABLE external_vehicles 
    ADD CONSTRAINT uq_external_vehicles_ext_vehicle 
    UNIQUE (external_company_vehicle_id);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Index for lookups
CREATE INDEX IF NOT EXISTS idx_external_vehicles_vehicle 
ON external_vehicles(vehicle_id);

CREATE INDEX IF NOT EXISTS idx_external_vehicles_ext_vehicle 
ON external_vehicles(external_company_vehicle_id);

-- =============================================
-- Insert Datatrack 247 as external company
-- =============================================
INSERT INTO external_companies (company_name, api_base_url, api_key_name, is_active)
VALUES ('Datatrack 247', 'https://datatrack247.com/api', 'Datatrack', true)
ON CONFLICT (company_name) DO UPDATE SET
    api_base_url = EXCLUDED.api_base_url,
    updated_at = NOW();

-- =============================================
-- Views for easy querying
-- =============================================

-- View: All external vehicles with company info
CREATE OR REPLACE VIEW v_external_vehicles_full AS
SELECT 
    ev.id AS link_id,
    ev.vehicle_id,
    ev.is_primary,
    ev.linked_at,
    ecv.id AS external_vehicle_id,
    ecv.external_id,
    ecv.name AS external_name,
    ecv.vin AS external_vin,
    ecv.license_plate AS external_plate,
    ecv.make,
    ecv.model,
    ecv.year,
    ecv.color,
    ecv.last_synced_at,
    ec.id AS external_company_id,
    ec.company_name
FROM external_vehicles ev
JOIN external_company_vehicles ecv ON ev.external_company_vehicle_id = ecv.id
JOIN external_companies ec ON ecv.external_company_id = ec.id;

-- View: Unlinked external vehicles (available for linking)
CREATE OR REPLACE VIEW v_unlinked_external_vehicles AS
SELECT 
    ecv.*,
    ec.company_name
FROM external_company_vehicles ecv
JOIN external_companies ec ON ecv.external_company_id = ec.id
WHERE ecv.id NOT IN (SELECT external_company_vehicle_id FROM external_vehicles)
AND ecv.is_active = true;

-- View: Our vehicles with their primary external tracker
CREATE OR REPLACE VIEW v_vehicles_with_tracker AS
SELECT 
    v.id AS vehicle_id,
    v.license_plate,
    v.vin,
    v.color,
    v.status,
    ecv.external_id AS tracker_serial,
    ecv.name AS tracker_name,
    ec.company_name AS tracker_company,
    ev.linked_at AS tracker_linked_at
FROM vehicles v
LEFT JOIN external_vehicles ev ON v.id = ev.vehicle_id AND ev.is_primary = true
LEFT JOIN external_company_vehicles ecv ON ev.external_company_vehicle_id = ecv.id
LEFT JOIN external_companies ec ON ecv.external_company_id = ec.id;

-- =============================================
-- Update trigger for updated_at
-- =============================================
CREATE OR REPLACE FUNCTION update_external_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_external_companies_updated ON external_companies;
CREATE TRIGGER tr_external_companies_updated
    BEFORE UPDATE ON external_companies
    FOR EACH ROW EXECUTE FUNCTION update_external_updated_at();

DROP TRIGGER IF EXISTS tr_external_company_vehicles_updated ON external_company_vehicles;
CREATE TRIGGER tr_external_company_vehicles_updated
    BEFORE UPDATE ON external_company_vehicles
    FOR EACH ROW EXECUTE FUNCTION update_external_updated_at();

DROP TRIGGER IF EXISTS tr_external_vehicles_updated ON external_vehicles;
CREATE TRIGGER tr_external_vehicles_updated
    BEFORE UPDATE ON external_vehicles
    FOR EACH ROW EXECUTE FUNCTION update_external_updated_at();

-- =============================================
-- Migration complete
-- =============================================
