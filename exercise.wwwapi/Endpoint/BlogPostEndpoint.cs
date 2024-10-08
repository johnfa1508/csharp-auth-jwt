﻿using exercise.wwwapi.Configuration;
using exercise.wwwapi.DTO;
using exercise.wwwapi.DTOs;
using exercise.wwwapi.Helpers;
using exercise.wwwapi.Model;
using exercise.wwwapi.Models;
using exercise.wwwapi.Repository;
using exercise.wwwapi.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace exercise.wwwapi.Endpoint
{
    public static class BlogPostEndpoint
    {
        public static void ConfigureBlogPostEndpoint(this WebApplication app)
        {
            var blogpost = app.MapGroup("blogpost");

            blogpost.MapGet("/posts", GetPosts);
            blogpost.MapPost("/posts", CreatePost);
            blogpost.MapPut("/posts/{id}", UpdatePost);
            blogpost.MapPost("/register", Register);
            blogpost.MapPost("/login", Login);
            blogpost.MapGet("/users", GetUsers);
        }

        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        private static async Task<IResult> GetUsers(IDatabaseRepository<User> service)
        {
            return Results.Ok(service.GetAll());
        }

        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        private static async Task<IResult> UpdatePost(IDatabaseRepository<BlogPost> repository, ClaimsPrincipal user, BlogPostModel model, int id)
        {
            var userId = user.UserRealId();

            if (userId != null)
            {
                var targetPost = repository.GetById(id);

                if (model.Text != "")
                {
                    targetPost.Text = model.Text;
                }
                if (model.AuthorID != 0)
                {
                    targetPost.AuthorID = model.AuthorID;
                }

                repository.Update(targetPost);

                BlogPostDTO posted = new BlogPostDTO()
                {
                    Text = targetPost.Text,
                    AuthorID = targetPost.AuthorID
                };

                return TypedResults.Created("success!", posted);
            }
            else
            {
                return TypedResults.Unauthorized();
            }
        }

        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        private static async Task<IResult> CreatePost(IDatabaseRepository<BlogPost> repository, BlogPostModel model, ClaimsPrincipal user)
        {
            var userId = user.UserRealId();

            if (userId != null)
            {
                var inserted = repository.Insert(new BlogPost() 
                {
                    Text = model.Text,
                    AuthorID = (int) userId
                });

                BlogPostDTO posted = new BlogPostDTO()
                {
                    Text = model.Text,
                    AuthorID = model.AuthorID
                };

                return TypedResults.Created("success!", posted);
            }
            else
            {
                return Results.Unauthorized();
            }
        }

        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        private static async Task<IResult> GetPosts(IDatabaseRepository<BlogPost> repository, ClaimsPrincipal user)
        {
            var userId = user.UserRealId();

            if (userId != null)
            {
                var blogposts = repository.GetAll();
                Payload<List<BlogAllDTO>> payload = new Payload<List<BlogAllDTO>>();
                payload.data = new List<BlogAllDTO>();

                foreach (var blogpost in blogposts)
                {
                    payload.data.Add(new BlogAllDTO()
                    {
                        ID = blogpost.ID,
                        Text = blogpost.Text,
                        AuthorID = blogpost.AuthorID
                    });
                }
                payload.status = "success";
                return TypedResults.Ok(payload);
            }
            else
            {
                return Results.Unauthorized();
            }
        }

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        private static async Task<IResult> Register(UserRequestDto request, IDatabaseRepository<User> service)
        {

            //user exists
            if (service.GetAll().Where(u => u.Username == request.Username).Any()) return Results.Conflict(new Payload<UserRequestDto>() { status = "Username already exists!", data = request });

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User();

            user.Username = request.Username;
            user.PasswordHash = passwordHash;

            service.Insert(user);
            //service.Save();

            return Results.Ok(new Payload<string>() { data = "Created Account" });
        }

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        private static async Task<IResult> Login(UserRequestDto request, IDatabaseRepository<User> service, IConfigurationSettings config)
        {
            //user doesn't exist
            if (!service.GetAll().Where(u => u.Username == request.Username).Any()) return Results.BadRequest(new Payload<UserRequestDto>() { status = "User does not exist", data = request });

            User user = service.GetAll().FirstOrDefault(u => u.Username == request.Username)!;


            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.BadRequest(new Payload<UserRequestDto>() { status = "Wrong Password", data = request });
            }
            string token = CreateToken(user, config);

            return Results.Ok(new Payload<string>() { data = token });
        }

        private static string CreateToken(User user, IConfigurationSettings config)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Sid, user.ID.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.GetValue("AppSettings:Token")));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials
                );
            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }
    }
}
