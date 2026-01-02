# Testing Authentication with Keycloak

## Get Access Token from Keycloak

```bash
# Get token
curl -X POST http://localhost:9090/realms/myrealm/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=myclient" \
  -d "username=myuser" \
  -d "password=1234" \
  -d "client_secret=YOUR_CLIENT_SECRET"
```

## Test Basket Endpoint with Token

```bash
# Extract just the access_token from the response
ACCESS_TOKEN="your_access_token_here"

# Test authenticated endpoint
curl -X GET http://localhost:6000/basket/myuser \
  -H "Authorization: Bearer $ACCESS_TOKEN"

# Or test checkout
curl -X POST http://localhost:6000/basket/checkout \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "basketCheckout": {
      "userName": "myuser",
      "customerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "totalPrice": 100,
      "firstName": "John",
      "lastName": "Doe",
      "emailAddress": "john@example.com",
      "addressLine": "123 Main St",
      "country": "USA",
      "state": "CA",
      "zipCode": "12345",
      "cardName": "John Doe",
      "cardNumber": "1234567890123456",
      "expiration": "12/25",
      "cvv": "123",
      "paymentMethod": 1
    }
  }'
```

## Troubleshooting

If you still get authentication errors:
1. Check the token issuer matches one of the configured issuers
2. Verify the realm name is correct (myrealm)
3. Ensure the client exists in Keycloak
4. Check API logs: `docker-compose logs api -f`
