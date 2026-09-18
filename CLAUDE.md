# Electronics Accessories Backend — Claude ke liye hidayat

## Structure
- Backend: .NET 10 Web API (is repo ki root mein)
- Database: MySQL + Dapper (koi ORM nahi, koi migrations nahi)
- Tests: xUnit (46 unit tests pass, integration tests Docker chahte hain — skip hain)

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Test (cloud): `DOTNET_ROLL_FORWARD=LatestMajor dotnet test`

## Database
- MySQL + Dapper
- Schema changes team khud manually karti hai
- Connection string config mein hai — kabhi commit na karo
- Koi migration file mat banao

## Kaam ka tareeqa
- Har tabdeeli ke baad `dotnet build` aur `dotnet test` chalao
- 0 errors hone chahiye — warnings theek hain
- PR hamesha `master` branch ke liye banao
- Secrets ya passwords kabhi commit na karo
- Docker istemal na karo
