#!/bin/bash
set -e

PGDATA="/var/lib/postgresql/18/main"
export PGDATA
export PGPASSWORD="replicator"

mkdir -p "$PGDATA"
chown -R postgres:postgres /var/lib/postgresql
chmod 700 "$PGDATA"

if [ ! -f "$PGDATA/standby.signal" ]; then
    echo "Initializing replica from primary..."
    rm -rf "$PGDATA"/*
    
    gosu postgres pg_basebackup -h postgres-primary -D "$PGDATA" -U replicator -Fp -Xs -P -R || {
        echo "Waiting for primary to be ready..."
        sleep 5
        exec "$0"
    }
    
    touch "$PGDATA/standby.signal"
    chown postgres:postgres "$PGDATA/standby.signal"
    echo "Replica initialized successfully"
fi

echo "Starting PostgreSQL replica..."
exec gosu postgres postgres -c hot_standby=on
