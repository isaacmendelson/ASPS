## Test the ASPSBackend API

### 1. Create a User
POST https://localhost:7001/api/users
Content-Type: application/json

```json
{
  "keycloakUserId": "keycloak-test-001",
  "firstName": "John",
  "lastName": "Doe",
  "email": "john.doe@example.com",
  "role": 1
}
```

### 2. Get All Users
GET https://localhost:7001/api/users

### 3. Get User by Key
GET https://localhost:7001/api/users/User/{keyValue}
(Replace {keyValue} with the GUID returned from create)

### 4. Get User Details (with devices and accounts)
GET https://localhost:7001/api/users/User/{keyValue}/details

### 5. Update User
PUT https://localhost:7001/api/users/User/{keyValue}
Content-Type: application/json

```json
{
  "firstName": "John",
  "lastName": "Doe",
  "address": "123 Main St",
  "city": "New York",
  "phoneNumber": "+1-555-0100"
}
```

### 6. Create a Device for User
POST https://localhost:7001/api/userdevices
Content-Type: application/json

```json
{
  "userKeyType": "User",
  "userKeyValue": "{keyValue}",
  "deviceType": 1,
  "deviceUid": "PC-12345",
  "operatingSystem": 1
}
```

### 7. Delete User (soft delete)
DELETE https://localhost:7001/api/users/User/{keyValue}

---

## Using cURL

### Create User
```bash
curl -X POST https://localhost:7001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "keycloakUserId": "keycloak-test-001",
    "firstName": "John",
    "lastName": "Doe",
    "email": "john.doe@example.com",
    "role": 1
  }' \
  --insecure
```

### Get All Users
```bash
curl https://localhost:7001/api/users --insecure
```

---

## Using Swagger UI

1. Navigate to: https://localhost:7001/swagger
2. Expand the `/api/Users` section
3. Try the POST endpoint to create a user
4. Copy the returned Key value
5. Try GET endpoints with that Key

---

## Expected Flow

1. **POST /api/users** → Returns `{ "success": true, "message": "User created successfully", "data": { "key": "User|{guid}|" } }`
2. **GET /api/users** → Returns array with the created user
3. **GET /api/users/User/{guid}** → Returns user details
4. **POST /api/userdevices** → Creates a device for the user
5. **GET /api/users/User/{guid}/details** → Returns user with devices and accounts

---

## Troubleshooting

If you get errors:
- Check both console outputs (ASPSBackend and WebApi)
- Verify MySQL is running and database exists
- Check connection string in appsettings.json
- Ensure both projects are running (multi-startup)
