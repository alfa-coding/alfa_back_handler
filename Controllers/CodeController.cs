using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using alfa_back.lib.Infrastructure;
using alfa_back.lib.Models;
using alfa_back.lib.Utils;
using alfa_back_handler.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace alfa_back_handler.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CodeController : ControllerBase
    {
        private readonly ILogger<CodeController> _logger;
        public IConnector<Problem> ProblemsRepo { get; }
        public IConnector<DictionaryCodeFormater> DictionaryCodeFormRepo { get; }

        public CodeController(ILogger<CodeController> logger, IConnector<Problem> _problemsRepo, IConnector<DictionaryCodeFormater> _formater)
        {
            _logger = logger;
            this.ProblemsRepo = _problemsRepo;
            this.DictionaryCodeFormRepo = _formater;
        }


        [HttpPost]
        public async Task<PistonDTOResponse> Post([FromBody] ProblemDTO problemSolution)
        {

            Problem actualProblem = await this.ProblemsRepo.GetElementById(problemSolution.Id);
            DictionaryCodeFormater dictionaryForOutouts = this.DictionaryCodeFormRepo.GetElements().FirstOrDefault(d => d.Extension == problemSolution.Extension);
            var actualLangInfo = actualProblem.LanguageInformation.FirstOrDefault(p => p.Extension == problemSolution.Extension);

            var entryFromProblemText = actualLangInfo.EntryPoint;
            var headersText = actualLangInfo.HeadersAllowed;
            var dictionarOutputText = dictionaryForOutouts.DictionaryFormater;
            var userSolution = problemSolution.SourceCode;

            CodeContent code_elements = new CodeContent()
            {
                EntryPoint = entryFromProblemText,
                Header = headersText,
                DictionaryOutputs = dictionarOutputText,
                SolutionFunction = userSolution,
                Extension = problemSolution.Extension
            };
            var codeFactory = new FormaterCodeFactory();
            var code_ready = codeFactory.FormatCode(code_elements);

            var piston_obj= new PistonDTORequest()
            {
                language=problemSolution.Extension,
                version="5.0.201",
                files=new List<Code>(){
                    new Code(){
                        name="_cs_code",
                        content=code_ready
                    }
                }
        
            };
            string outputGotten = await RunCode(piston_obj);
            PistonDTOResponse decodedResponse = JsonSerializer.Deserialize<PistonDTOResponse>(outputGotten);


            return decodedResponse;
        }

        private Task<string> RunCode(PistonDTORequest code_ready)
        {
            //5.0.201
            //https://emkc.org/api/v2/piston/execute
            string pistonEndpoint = "https://emkc.org/api/v2/piston/execute";

            var client = new HttpClient();
            var jsonInString = JsonSerializer.Serialize(code_ready);
            return Task<string>.Run(() =>
                    {
                        var response = client.PostAsync(pistonEndpoint, new StringContent(jsonInString, Encoding.UTF8, "application/json"));
                        var contents = response.Result.Content.ReadAsStringAsync().Result;
                        _logger.LogInformation(contents);
                        return contents;
                    });
        }
    }

    public class Code
    {
        public string name { get; set; }
        public string content { get; set; }
        public string encoding { get; set; }
    }
    public class PistonDTORequest
    {
        
        public string language { get; set; }
        public string version { get; internal set; }
        public List<Code> files { get; set; }
        public string stdin { get; set; }
        public string[] args { get; set; }
        public int compile_timeout { get; set; }
        public int run_timeout { get; set; }
        public int compile_memory_limit { get; set; }
        public int run_memory_limit { get; set; }
    }


    public class PistonInfo
    {
        public string stdout { get; set; }
        public string stderr { get; set; }
        public string output { get; set; }  
        public int code { get; set; }
        public string signal { get; set; }
       
    }
    public class PistonDTOResponse
    {
        public string language { get; set; }
        public string version { get; set; }
        public PistonInfo run { get; set; }
        public PistonInfo compile { get; set; }
        
    }
}