using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Genesis.Receitas
{
    /// <summary>
    /// Um perfil salvo de uma receita (conjunto de opções que o usuário quer reaproveitar).
    /// Fica em %AppData%\Genesis\perfis\<receita-id>\<nome>.xml.
    /// </summary>
    public class PerfilReceita
    {
        public string ReceitaId { get; set; }      // "Adensador", "SubsistemaFlotador"
        public string Nome { get; set; }            // "Padrão ETE pequena"
        public string Descricao { get; set; }
        public DateTime CriadoEm { get; set; }
        public string OpcoesXml { get; set; }       // opções serializadas em XML
    }

    /// <summary>
    /// Gerencia os perfis: listar, salvar, apagar. Perfis ficam em disco (globais), separados do .genesis.
    /// </summary>
    internal static class PerfisReceita
    {
        public static string Pasta
        {
            get { return Path.Combine(Cfg.PastaApp, "perfis"); }
        }

        public static string PastaDaReceita(string receitaId)
        {
            string pasta = Path.Combine(Pasta, Sanitizar(receitaId));
            Directory.CreateDirectory(pasta);
            return pasta;
        }

        /// <summary>Lista todos os perfis salvos de uma receita, ordenados por nome.</summary>
        public static List<PerfilReceita> Listar(string receitaId)
        {
            var lista = new List<PerfilReceita>();
            try
            {
                string pasta = PastaDaReceita(receitaId);
                foreach (string arq in Directory.GetFiles(pasta, "*.xml"))
                {
                    try
                    {
                        var ser = new XmlSerializer(typeof(PerfilReceita));
                        using (var fs = File.OpenRead(arq))
                            lista.Add((PerfilReceita)ser.Deserialize(fs));
                    }
                    catch { /* perfil corrompido: ignora */ }
                }
            }
            catch { /* pasta não criada ainda: lista vazia */ }
            return lista.OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Salva um perfil (sobrescreve se o nome já existe).</summary>
        public static void Salvar(PerfilReceita perfil)
        {
            if (perfil == null || string.IsNullOrEmpty(perfil.ReceitaId) || string.IsNullOrEmpty(perfil.Nome))
                throw new ArgumentException("Perfil precisa ter ReceitaId e Nome.");
            perfil.CriadoEm = DateTime.Now;
            string pasta = PastaDaReceita(perfil.ReceitaId);
            string arq = Path.Combine(pasta, Sanitizar(perfil.Nome) + ".xml");
            var ser = new XmlSerializer(typeof(PerfilReceita));
            using (var fs = File.Create(arq))
                ser.Serialize(fs, perfil);
        }

        /// <summary>Apaga um perfil do disco.</summary>
        public static void Apagar(PerfilReceita perfil)
        {
            if (perfil == null) return;
            try
            {
                string arq = Path.Combine(PastaDaReceita(perfil.ReceitaId), Sanitizar(perfil.Nome) + ".xml");
                if (File.Exists(arq)) File.Delete(arq);
            }
            catch { /* sem permissão: ignora */ }
        }

        /// <summary>Serializa um objeto de opções para string XML.</summary>
        public static string Serializar(object opcoes, Type tipo)
        {
            if (opcoes == null || tipo == null) return null;
            var ser = new XmlSerializer(tipo);
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
                ser.Serialize(sw, opcoes);
            return sb.ToString();
        }

        /// <summary>Desserializa um XML de volta para o tipo.</summary>
        public static object Desserializar(string xml, Type tipo)
        {
            if (string.IsNullOrEmpty(xml) || tipo == null) return null;
            var ser = new XmlSerializer(tipo);
            using (var sr = new StringReader(xml))
                return ser.Deserialize(sr);
        }

        private static string Sanitizar(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return "sem_nome";
            var invalidos = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in nome)
                sb.Append(Array.IndexOf(invalidos, c) >= 0 ? '_' : c);
            return sb.ToString().Trim();
        }
    }
}