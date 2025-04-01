drop table if exists worker_zone_assignment;
drop table if exists worker;
drop table if exists "zone";

-- Create tables
CREATE TABLE worker (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    code VARCHAR(200) NOT NULL
);
CREATE UNIQUE INDEX worker_index ON worker(code);

CREATE TABLE zone (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    code VARCHAR(200) NOT NULL
);
CREATE UNIQUE INDEX zone_index ON worker(code);

CREATE TABLE public.worker_zone_assignment (
	id serial4 NOT NULL,
	worker_id int4 NOT NULL,
	zone_id int4 NOT NULL,
	effective_date date NOT NULL,
	CONSTRAINT worker_effective_date_unique UNIQUE (worker_id, effective_date),
	CONSTRAINT worker_zone_assignment_pkey PRIMARY KEY (id)
);


-- public.worker_zone_assignment foreign keys

ALTER TABLE public.worker_zone_assignment ADD CONSTRAINT worker_zone_assignment_worker_id_fkey FOREIGN KEY (worker_id) REFERENCES public.worker(id) ON DELETE CASCADE;
ALTER TABLE public.worker_zone_assignment ADD CONSTRAINT worker_zone_assignment_zone_id_fkey FOREIGN KEY (zone_id) REFERENCES public."zone"(id) ON DELETE CASCADE;

CREATE UNIQUE INDEX worker_zone_assignment_index ON worker_zone_assignment(worker_id,zone_id,effective_date);

-- Insert 50K workers
INSERT INTO worker (name, code)
SELECT 'W' || g, 'W' || g
FROM generate_series(1, 50000) g;

-- Insert 1K zones
INSERT INTO zone (name, code)
SELECT 'Z' || g, 'Z' || g
FROM generate_series(1, 1000) g;

-- Insert 300K worker assignments ensuring unique worker-date combination
INSERT INTO worker_zone_assignment (worker_id, zone_id, effective_date)
SELECT DISTINCT ON (worker_id, effective_date)
    worker_id,
    zone_id,
    effective_date
FROM (
    SELECT 
        FLOOR(RANDOM() * 50000) + 1 AS worker_id,  -- Random worker ID
        FLOOR(RANDOM() * 1000) + 1 AS zone_id,  -- Random zone ID
        (CURRENT_DATE - (FLOOR(RANDOM() * 365)::int)) AS effective_date  -- Ensure integer subtraction
    FROM generate_series(1, 600000)  -- Generate more rows to ensure uniqueness
) subquery
LIMIT 300000;









-- public.file_history definition

-- Drop table

-- DROP TABLE public.file_history;

CREATE TABLE public.file_history (
	id serial4 NOT NULL,
	"name" varchar NULL,
	processed_at date NULL,
	processed_records int4 NULL,
	inserted_records int4 NULL,
	CONSTRAINT file_history_pkey PRIMARY KEY (id)
);
