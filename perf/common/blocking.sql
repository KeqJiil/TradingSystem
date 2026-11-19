SELECT r.session_id,
       r.blocking_session_id,
       r.wait_type,
       r.wait_time AS wait_ms,
       r.wait_resource,
       t.text
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.blocking_session_id <> 0;
