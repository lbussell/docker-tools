// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.DotNet.DockerTools.Automation.Helpers;

/// <summary>
/// Utility for creating JWT tokens for GitHub App authentication.
/// </summary>
public static class JwtHelper
{
    /// <summary>
    /// Creates a JWT token using the provided issuer and base64-encoded RSA private key (PEM format).
    /// </summary>
    /// <param name="issuer">The GitHub App's client ID (used as the JWT issuer).</param>
    /// <param name="base64PrivateKey">The RSA private key in base64-encoded PEM format.</param>
    /// <param name="timeout">How long the JWT should be valid.</param>
    /// <returns>The signed JWT string.</returns>
    public static string CreateJwt(string issuer, string base64PrivateKey, TimeSpan timeout)
    {
        var privateKey = DecodeBase64String(base64PrivateKey);

        RsaSecurityKey rsaSecurityKey;
        using (var rsa = RSA.Create(4096))
        {
            rsa.ImportFromPem(privateKey);
            rsaSecurityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        }

        var now = DateTime.UtcNow;
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            IssuedAt = now,
            Expires = now + timeout,
            Issuer = issuer,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.CreateJwtSecurityToken(descriptor);
        return tokenHandler.WriteToken(jwt);
    }

    private static string DecodeBase64String(string input)
    {
        var data = Convert.FromBase64String(input);
        return System.Text.Encoding.UTF8.GetString(data);
    }
}
