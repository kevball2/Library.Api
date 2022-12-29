using Library.Api.Data;
using Library.Api.Models;
using Library.Api.Services;
using FluentValidation;
using Library.Api.Validators;
using FluentValidation.Results;
using Library.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Library.Api.Extensions;
using Microsoft.AspNetCore.Cors;


// default args used to create builder
//var builder = WebApplication.CreateBuilder(args);

// We can call many different WebApplicationOptions to configure Builder
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    //WebRootPath = "./wwwroot", // customize the root folder
    //EnvironmentName = Environment.GetEnvironmentVariable("env"), // determine environmentName from a custom env variable
    //ApplicationName = "Library.Api" // Modify the name of the application (useful for logging)
});

// To Customize how JSON is Serialized from client object to C# objecct, configure JsonOptions
//builder.Services.Configure<JsonOptions>(options =>
//{
//    options.SerializerOptions.PropertyNameCaseInsensitive = true;
//    options.SerializerOptions.IncludeFields = true;
//});

// Adding CORS (Cross Origin Resource Servicing) to builder
// and setting options
builder.Services.AddCors(options =>
{
    options.AddPolicy("AnyOrigin", x => x.AllowAnyOrigin());
});



// To add additional settings files you can load them via builder.Configuratio
builder.Configuration.AddJsonFile("appsettings.Local.json", true, true);

// Adding Authentication, need to list the scheme, options and method to submit claims and get an identity back
builder.Services.AddAuthentication(ApiKeySchemeConstants.SchemeName)
    .AddScheme<ApiKeyAuthSchemeOptions, ApiKeyAuthHandler>(ApiKeySchemeConstants.SchemeName, _ => { }) ;

// Allows for authorization based on the authetication by users
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqliteConnectionFactory(
        builder.Configuration.GetValue<string>("Database:ConnectionString")));
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<IBookService, BookService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

// Adding Swagger OpenAPI documentation and Webpage
app.UseSwagger();
app.UseSwaggerUI();

// enabling cors based on our builder configuration
// Cors can be set on any API endpoint using [EnableCors("<PolicyName>")]
app.UseCors();

// Authentication is set for all app calls after this point. 
// Since swagger is added Before the authentiion call,
// there is no security required to access it.
app.UseAuthorization();


// Authentication can be set for all endpoints or selectively. 
// you can selectively enable or exclude any endpoint by adding the properties below

app.MapPost("books",
    //[Authorize(AuthenticationSchemes = ApiKeySchemeConstants.SchemeName)]
    //[AllowAnonymous] 
    async (Book book, IBookService bookService, 
    IValidator<Book> validator, LinkGenerator linker,
    HttpContext contex) =>
{

    var validationResult = await validator.ValidateAsync(book);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(validationResult.Errors);
    }

    var created = await bookService.CreateAsync(book);
    if(!created)
    {
        return Results.BadRequest(new List<ValidationFailure>
        {
            new ("Isbn", "A book with this ISBN-13 already exists")
        });
    }


    // Another option to generate with LinkGenerator and HttpContext to 
    // Pass back the full URl to the client
    // var path = linker.GetPathByName("GetBook", new { isbn = book.Isbn })!;
    var locationUri = linker.GetUriByName(contex, "GetBook", new { isbn = book.Isbn });
    return Results.Created(locationUri, book);
    // New Return route is based on the endpoint WithName. 
    //return Results.CreatedAtRoute("GetBook", new { isbn = book.Isbn }, book);
    // Brittle option with hard coded values
    //return Results.Created($"/books/{book.Isbn}", book);
}).WithName("CreateBook")
.Accepts<Book>("application/json")
.Produces<Book>(201)
.Produces<IEnumerable<ValidationFailure>>(400)
.WithTags("Books");//.AllowAnonymous();

// WithName is used to give friendly names to links generated without hardcoding values

app.MapPut("books/{isbn}", async (Book book, IBookService bookService,
    IValidator<Book> validator, string isbn) =>
{
    book.Isbn = isbn;
    var validationResult = await validator.ValidateAsync(book);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(validationResult.Errors);
    }

    var updated = await bookService.UpdateAsync(book);
    return updated ? Results.Ok(book) : Results.NotFound();

    
})
.WithName("UpdateBook")
.Accepts<Book>("application/json")
.Produces<Book>(200)
.Produces<IEnumerable<ValidationFailure>>(400)
.WithTags("Books");

app.MapGet("books", async (IBookService BookService, string? searchTerm) =>
{
    if (searchTerm is not null && !string.IsNullOrWhiteSpace(searchTerm))
    {
        var matchedBooks = await BookService.SearchByTitleAsync(searchTerm);
        return Results.Ok(matchedBooks);
    }

    var books = await BookService.GetAllAsync();
    return Results.Ok(books);
})
.WithName("GetBooks")
.Produces<IEnumerable<BookService>>(200)
.WithTags("Books");


app.MapGet("books/{isbn}", async (string isbn, IBookService BookService) =>
{
    var book = await BookService.GetByIsbnAsync(isbn);
    return book is not null ?  Results.Ok(book) : Results.NotFound();
})
.WithName("GetBook")
.Produces<Book>(200)
.Produces(404)
.WithTags("Books");

app.MapDelete("books/{isbn}", async (string isbn, IBookService BookService) =>
{
    var deleted = await BookService.DeleteAsync(isbn);
     
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteBook")
.Produces(204)
.Produces(404)
.WithTags("Books");


// To be able to pass HTML back through The Results object we need to create a custom
// extension to allow for the proper parsing of HTML. This is done in ResultExtensions.cs
app.MapGet("status", [EnableCors("AnyOrigin")] () =>
{
    return Results.Extensions.html(@"<!doctype hotml>
<html>
    <head><title>Status Page</title></head>
    <body>
        <h1>Status</h1>
        <b>The server is working fine. Bye Bye!</b>
    </body>
</html>");
})
.ExcludeFromDescription(); // This will remove the endpoint from the Swagger page
// alternate way to set Cors policies
//.RequireCors("AnyOrigin")

var databaseInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
await databaseInitializer.InitializeAsync();

app.Run();
