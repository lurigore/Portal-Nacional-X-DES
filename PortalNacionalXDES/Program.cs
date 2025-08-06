using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

class Program
{
    static string XML_FOLDER = @"C:\XML"; // Pasta onde estão os XMLs
    static string imTomador = "";
    static void Main()
    {
        ProcessarPasta(XML_FOLDER);
    }

    static void ProcessarPasta(string xmlFolder)
    {
        if (!Directory.Exists(xmlFolder))
        {
            Console.WriteLine($"Pasta '{xmlFolder}' não encontrada. Certifique-se de que ela existe.");
            return;
        }

        var arquivosXml = Directory.GetFiles(xmlFolder, "*.xml");
        if (arquivosXml.Length == 0)
        {
            Console.WriteLine("Nenhum arquivo .xml encontrado na pasta.");
            return;
        }

        var registrosH = new Dictionary<string, string>();
        var registrosR = new List<string>();

        foreach (var path in arquivosXml)
        {
            try
            {
                var (h, r, cnpj) = GerarRegistros(path);
                if (!registrosH.ContainsKey(cnpj))
                    registrosH[cnpj] = h;

                registrosR.Add(r);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar {path}: {ex.Message}");
            }
        }

        string arquivoSaida = Path.Combine(xmlFolder, "registros_des.txt");
        File.WriteAllLines(arquivoSaida, registrosH.Values.Concat(registrosR));

        Console.WriteLine($"Processamento finalizado. Arquivo gerado em: {arquivoSaida}\n");
    }

    static (string registroH, string registroR, string cnpjTomador) GerarRegistros(string xmlPath)
    {
        try
        {

            XDocument doc = XDocument.Load(xmlPath);
            XNamespace nf = "http://www.sped.fazenda.gov.br/nfse";

            var toma = doc.Descendants(nf + "toma").FirstOrDefault();
            var emit = doc.Descendants(nf + "emit").FirstOrDefault();
            var prest = doc.Descendants(nf + "prest").FirstOrDefault();
            string opcaoSimples = prest?.Descendants(nf + "opSimpNac").FirstOrDefault()?.Value ?? "";
            var infDPS = doc.Descendants(nf + "infDPS").FirstOrDefault();
            var valores = doc.Descendants(nf + "valores").FirstOrDefault();
            var valorServ = doc.Descendants(nf + "vServPrest").FirstOrDefault();
            var localIncid = doc.Descendants(nf + "cLocIncid").FirstOrDefault();
            var trib = doc.Descendants(nf + "tribMun").FirstOrDefault();
            var serv = doc.Descendants(nf + "cServ").FirstOrDefault();

            //possibilidades
            string[] sociedadeCGC = {
                "19179789000118",
                "65165649000108"
            };
            string[] construcao = {
                "070201",
                "070202",
                "070203",
                "070204",
                "070205"
            };
            string[] propaganda = {
                "170601",
                "170602"
            };

            imTomador = toma?.Element(nf + "IM")?.Value ?? "";
            if (imTomador == null)
            {
                Console.WriteLine("Insira a Inscrição Municial do tomador");
                imTomador = Console.ReadLine() ?? "";
            }

            string cnpjTomador = toma?.Element(nf + "CNPJ")?.Value ?? "";
            string xNomeTomador = toma?.Element(nf + "xNome")?.Value ?? "";

            string dataAtual = DateTime.Now.ToString("dd/MM/yyyyHH:mm:ss");
            string versaoSistema = "VERSÃO301 BUILD152";

            string registroH = $"H|{dataAtual}||{versaoSistema}|{imTomador}|{cnpjTomador}||{xNomeTomador}|{xNomeTomador}|||0|2|2|2|||2|2|null";

            // === Registro R ===
            string dhEmissao = infDPS?.Element(nf + "dhEmi")?.Value ?? "";
            string dataEmissao = "";
            if (DateTime.TryParse(dhEmissao, null, DateTimeStyles.AdjustToUniversal, out DateTime dt))
                dataEmissao = dt.ToString("ddMMyyyy");

            
            string serie = "0";
            string numeroNF = doc.Descendants(nf + "nNFSe").FirstOrDefault()?.Value ?? "";
            string valorTotal = valorServ?.Element(nf + "vServ")?.Value ?? "";

            var tomaEnd = toma?.Descendants(nf + "endNac").FirstOrDefault();
            var prestEnd = emit?.Descendants(nf + "enderNac").FirstOrDefault();

            bool isMei = (opcaoSimples == "2");
            /*
            DES - 
            1 - Simples Nacional
            2 - Não Optante
            3 - MEI

            Portal Nacional - 
            1 - Não Optante
            2 - MEI
            3 - Simples Nacional
            */
            string opcao = opcaoSimples switch
            {
                "3" => "1",
                "2" => "3",
                "1" => "2",
                _ => "2"
            };
            string modelo = isMei ? "28" : "5";

            string situacaoResponsabilidade = "1";
            string codServ = serv?.Element(nf + "cTribNac")?.Value ?? "";
            foreach (string servico in construcao)
            {
                situacaoResponsabilidade = (codServ == servico) ? "3" : "1";
                break;
            }
            foreach (string servico in propaganda)
            {
                situacaoResponsabilidade = (codServ == servico) ? "5" : "1";
                break;
            }


            string ufEmitente = prestEnd?.Element(nf + "UF")?.Value ?? "";
            string codMunEmitente = prestEnd?.Element(nf + "cMun")?.Value ?? "";
            string localIncidencia = localIncid?.Value ?? "";

            string aliquotaIss = trib?.Element(nf + "pAliq")?.Value ?? "0.00";
            bool isRetido = aliquotaIss != "0.00";
            string retencao = isRetido ? "1" : "2";

            string motivoNaoRetencao = "1";
            
            if (isMei)
            {
                motivoNaoRetencao = "14";
            }
            else if (!isMei && !isRetido)
            {
                motivoNaoRetencao = "1";
                string semPrefixo = numeroNF.Substring(2);
                numeroNF = "2025" + semPrefixo.TrimStart('0');
            }
            else if (isRetido)
            {
                motivoNaoRetencao = "16";
                string semPrefixo = numeroNF.Substring(2);
                numeroNF = "2025" + semPrefixo.TrimStart('0');
            }
            foreach (string cadastro in sociedadeCGC)
            {
                if (emit?.Element(nf + "CNPJ")?.Value == cadastro)
                {
                    motivoNaoRetencao = "6";
                }
            }

            var camposR = new List<string>
            {
                "R",
                dataEmissao,
                dataEmissao,
                modelo,
                serie,
                "",
                situacaoResponsabilidade,
                motivoNaoRetencao,
                localIncidencia,
                retencao,
                numeroNF,
                valorTotal,
                valorTotal,
                aliquotaIss,
                opcao,
                "",
                emit?.Element(nf + "CNPJ")?.Value ?? "",
                "",
                emit?.Element(nf + "xNome")?.Value ?? "",
                prestEnd?.Element(nf + "xLgr")?.Value ?? "",
                prestEnd?.Element(nf + "nro")?.Value ?? "",
                "",
                prestEnd?.Element(nf + "xBairro")?.Value ?? "",
                codMunEmitente,
                "1058",
                prestEnd?.Element(nf + "CEP")?.Value ?? "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                localIncidencia,
                localIncidencia,
                "1058",
                "",
                "",
                ""
            };

            string registroR = string.Join("|", camposR);
            return (registroH, registroR, cnpjTomador);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao processar {xmlPath}: {ex.Message}");
        }
    }
}
