USE OrderPerf;

SELECT l.request_session_id AS session_id,
       l.resource_type,
       l.request_mode,
       l.request_status,
       COALESCE(OBJECT_NAME(p.object_id), OBJECT_NAME(IIF(l.resource_type = 'OBJECT', l.resource_associated_entity_id, NULL))) AS object_name,
       i.name AS index_name,
       COUNT(*) AS locks
FROM sys.dm_tran_locks AS l
LEFT JOIN sys.partitions AS p ON l.resource_type IN ('KEY', 'PAGE', 'RID', 'HOBT') AND p.hobt_id = l.resource_associated_entity_id
LEFT JOIN sys.indexes AS i ON i.object_id = p.object_id AND i.index_id = p.index_id
WHERE l.resource_database_id = DB_ID()
  AND l.request_session_id <> @@SPID
  AND l.resource_type <> 'DATABASE'
GROUP BY l.request_session_id, l.resource_type, l.request_mode, l.request_status, p.object_id, i.name, l.resource_associated_entity_id
ORDER BY l.request_session_id, l.resource_type, index_name;
