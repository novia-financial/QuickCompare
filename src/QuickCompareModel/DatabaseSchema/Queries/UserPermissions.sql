SELECT		[UserName] =
				CASE princ.[type]
				WHEN 'S' THEN princ.[name]
				WHEN 'U' THEN ulogin.[name]
				COLLATE Latin1_General_CI_AI END,
			[UserType] =
				CASE princ.[type]
				WHEN 'S' THEN 'SQL User'
				WHEN 'U' THEN 'Windows User' END,
			[USER_NAME] = princ.[name],
			[ROLE_NAME] = null,
			[PERMISSION_TYPE] = perm.[permission_name],
			[PERMISSION_STATE] = perm.[state_desc],
			[OBJECT_TYPE] = obj.type_desc,
			[OBJECT_NAME] = OBJECT_NAME(perm.major_id),
			[COLUMN_NAME] = col.[name]
FROM		sys.database_principals princ
LEFT JOIN	sys.login_token ulogin
				ON princ.[sid] = ulogin.[sid]
LEFT JOIN	sys.database_permissions perm
				ON perm.[grantee_principal_id] = princ.[principal_id]
LEFT JOIN	sys.columns col
				ON col.[object_id] = perm.major_id AND col.[column_id] = perm.[minor_id]
LEFT JOIN	sys.objects obj
				ON perm.[major_id] = obj.[object_id]
WHERE		princ.[type] in ('S','U')
