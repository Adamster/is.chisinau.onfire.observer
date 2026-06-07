alter table fire_incidents
    add column if not exists source_url text not null default '';
