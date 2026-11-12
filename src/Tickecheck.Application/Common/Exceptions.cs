namespace Tickecheck.Application.Common;

// Each exception maps to one HTTP status code in the API's GlobalExceptionHandler.
// Use cases throw them; they never build HTTP responses themselves.

/// <summary>The resource does not exist, or the user is not allowed to know it exists (404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The request conflicts with existing data, like a duplicated email (409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Credentials or tokens are missing or invalid (401).</summary>
public class UnauthorizedException(string message) : Exception(message);

/// <summary>The user is known but may not do this (403).</summary>
public class ForbiddenException(string message) : Exception(message);
