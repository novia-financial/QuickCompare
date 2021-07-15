SELECT		[UserName] =
				CASE memberprinc.[type]
				WHEN 'S' THEN memberprinc.[name]
				WHEN 'U' THEN ulogin.[name]
				COLLATE Latin1_General_CI_AI END,
			[UserType] =
				CASE memberprinc.[type]
				WHEN 'S' THEN 'SQL User'
				WHEN 'U' THEN 'Windows User' END,
			[USER_NAME] = memberprinc.[name],
			[ROLE_NAME] = roleprinc.[name],
			[PERMISSION_TYPE] = perm.[permission_name],
			[PERMISSION_STATE] = perm.[state_desc],
			[OBJECT_TYPE] = obj.type_desc,
			[OBJECT_NAME] = OBJECT_NAME(perm.major_id),
			[COLUMN_NAME] = col.[name]
FROM		sys.database_role_members members
JOIN		sys.database_principals roleprinc
				ON roleprinc.[principal_id] = members.[role_principal_id]
JOIN		sys.database_principals memberprinc
				ON memberprinc.[principal_id] = members.[member_principal_id]
LEFT JOIN	sys.login_token ulogin
				ON memberprinc.[sid] = ulogin.[sid]
LEFT JOIN	sys.database_permissions perm
				ON perm.[grantee_principal_id] = roleprinc.[principal_id]
LEFT JOIN	sys.columns col
				ON col.[object_id] = perm.major_id AND col.[column_id] = perm.[minor_id]
LEFT JOIN	sys.objects obj
				ON perm.[major_id] = obj.[object_id]
