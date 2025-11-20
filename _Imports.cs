global using System;
global using System.Collections;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.Collections.ObjectModel;
global using System.ComponentModel.DataAnnotations.Schema;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.Linq;
global using System.Net;
global using System.Net.Http.Headers;
global using System.Reflection;
global using System.Security.Claims;
global using System.Security.Cryptography;
global using System.Security.Cryptography.X509Certificates;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;

global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Authentication.Cookies;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.Components;
global using Microsoft.AspNetCore.Components.Web;
global using Microsoft.AspNetCore.Connections;
global using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
global using Microsoft.AspNetCore.Http.Extensions;
global using Microsoft.AspNetCore.HttpOverrides;
global using Microsoft.AspNetCore.Localization;
global using Microsoft.AspNetCore.Routing.Matching;

global using Microsoft.CodeAnalysis;
global using Microsoft.CodeAnalysis.CSharp.Scripting;
global using Microsoft.CodeAnalysis.Scripting;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Design;
global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

global using Microsoft.Extensions.Caching.Memory;
global using Microsoft.Extensions.Localization;
global using Microsoft.Extensions.Logging.Abstractions;
global using Microsoft.Extensions.Logging.Console;
global using Microsoft.Extensions.Options;

global using Microsoft.JSInterop;

global using Yarp.ReverseProxy.Configuration;
global using Yarp.ReverseProxy.Model;
global using Yarp.ReverseProxy.Transforms;

global using ByProxy.Data;
global using ByProxy.Infrastructure;
global using ByProxy.Infrastructure.Acme;
global using ByProxy.Infrastructure.AcmeDnsProvider;
global using ByProxy.Models;
global using ByProxy.Middleware;
global using ByProxy.Middleware.Auth;
global using ByProxy.Middleware.Proxy;
global using ByProxy.Services;
global using ByProxy.Utility;

global using ByProxy.AdminApp;
global using ByProxy.AdminApp.Components;
global using ByProxy.AdminApp.Enums;
global using ByProxy.AdminApp.Languages;
global using ByProxy.AdminApp.Services;