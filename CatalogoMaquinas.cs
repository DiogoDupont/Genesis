using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Genesis
{
    public class DefinicaoComponente
    {
        public string Categoria { get; set; } = "Químico";
        public string Codigo { get; set; } = "";
        public string Nome { get; set; } = "";
        public string TipoPadrao { get; set; } = "Partida diafragma";
        public string DescricaoPadrao { get; set; } = "";
        // Existente = usa os geradores nativos; Personalizada = definição cadastrada, ainda sem gerador nativo.
        public string PerfilGeracao { get; set; } = "Existente";
        public bool Ativo { get; set; } = true;
        public bool IncluirPorPadrao { get; set; }
        public bool AgitadorPorPadrao { get; set; }
        public bool PodePid { get; set; }
        public string ModulacaoPadrao { get; set; } = "Sem modulação";
        public string NivelPadrao { get; set; } = "Nível baixo digital";
    }

    [XmlRoot("CatalogoGenesis")]
    public class CatalogoConfiguravel
    {
        [XmlArrayItem("Componente")]
        public List<DefinicaoComponente> Itens { get; set; } = new List<DefinicaoComponente>();
    }

    internal static class CatalogoMaquinas
    {
        private static CatalogoConfiguravel _dados;
        private static string Arquivo { get { return Path.Combine(Cfg.PastaApp, "catalogo-maquinas.xml"); } }

        public static CatalogoConfiguravel Dados
        {
            get { if (_dados == null) Carregar(); return _dados; }
        }

        public static void Carregar()
        {
            try
            {
                if (File.Exists(Arquivo))
                {
                    using (var fs = File.OpenRead(Arquivo))
                        _dados = (CatalogoConfiguravel)new XmlSerializer(typeof(CatalogoConfiguravel)).Deserialize(fs);
                }
            }
            catch { _dados = null; }
            if (_dados == null || _dados.Itens == null || _dados.Itens.Count == 0)
                _dados = Padrao();
            // Corrige a nomenclatura conhecida sem sobrescrever uma escolha personalizada já salva.
            var des = _dados.Itens.FirstOrDefault(x => x.Categoria == "Químico" && x.Codigo == "DES");
            if (des != null && !_dados.Itens.Any(x => x.Categoria == "Químico" && x.Codigo == "DSN")) des.Codigo = "DSN";
        }

        private static CatalogoConfiguravel Padrao()
        {
            var c = new CatalogoConfiguravel();
            c.Itens.AddRange(new[] {
                Q("ALC", "ALCALINIZANTE", true, true, "Partida diafragma", "BOMBA DOSAGEM {NOME}"),
                Q("FLC", "FLOCULANTE", true, true, "Partida inversor por rede", "BOMBA DOSAGEM {NOME}"),
                Q("COA", "COAGULANTE", true, true, "Partida inversor por rede", "BOMBA DOSAGEM {NOME}"),
                Q("ANT", "ANTIESPUMANTE", true, false, "Partida diafragma", "BOMBA DOSAGEM {NOME}"),
                Q("ACI", "ÁCIDO", true, false, "Partida diafragma", "BOMBA DOSAGEM {NOME}"),
                Q("DSN", "DESINFETANTE", true, false, "Partida diafragma", "BOMBA DOSAGEM {NOME}")
            });
            c.Itens.Add(new DefinicaoComponente { Categoria = "Equipamento", Codigo = "BBA", Nome = "Bomba", TipoPadrao = "Partida direta", DescricaoPadrao = "BOMBA {DESC}", PerfilGeracao = "Existente" });
            c.Itens.Add(new DefinicaoComponente { Categoria = "Equipamento", Codigo = "AGT", Nome = "Agitador", TipoPadrao = "Partida direta", DescricaoPadrao = "AGITADOR {DESC}", PerfilGeracao = "Existente" });
            return c;
        }

        private static DefinicaoComponente Q(string codigo, string nome, bool ativo, bool incluir, string tipo, string descricao)
        { return new DefinicaoComponente { Categoria = "Químico", Codigo = codigo, Nome = nome, Ativo = ativo, IncluirPorPadrao = incluir, AgitadorPorPadrao = codigo == "ALC", PodePid = codigo == "ALC" || codigo == "ACI", ModulacaoPadrao = codigo == "ALC" ? QuimicoFlotador.ModPid : ((codigo == "FLC" || codigo == "COA") ? QuimicoFlotador.ModVazao : QuimicoFlotador.ModNenhuma), TipoPadrao = tipo, DescricaoPadrao = descricao, PerfilGeracao = "Existente" }; }

        public static void Salvar()
        {
            Directory.CreateDirectory(Cfg.PastaApp);
            string tmp = Arquivo + ".tmp";
            using (var fs = File.Create(tmp)) new XmlSerializer(typeof(CatalogoConfiguravel)).Serialize(fs, Dados);
            if (File.Exists(Arquivo)) File.Delete(Arquivo);
            File.Move(tmp, Arquivo);
        }

        public static List<QuimicoFlotador> QuimicosPadrao()
        {
            return Dados.Itens.Where(x => x.Categoria == "Químico" && x.Ativo)
                .Select(x => new QuimicoFlotador { Codigo = x.Codigo, Nome = x.Nome.ToUpperInvariant(), TipoBomba = x.TipoPadrao, DescricaoBomba = x.DescricaoPadrao, Incluir = x.IncluirPorPadrao, Agitador = x.AgitadorPorPadrao, PodePid = x.PodePid, Modulacao = string.IsNullOrWhiteSpace(x.ModulacaoPadrao) ? QuimicoFlotador.ModNenhuma : x.ModulacaoPadrao, Nivel = string.IsNullOrWhiteSpace(x.NivelPadrao) ? QuimicoFlotador.NivelDigital : x.NivelPadrao })
                .ToList();
        }
    }

    internal sealed class CatalogoMaquinasForm : Form
    {
        private readonly BindingList<DefinicaoComponente> _itens;
        private readonly DataGridView _grade;
        public CatalogoMaquinasForm()
        {
            Text = "Genesis — padrões de máquinas e componentes";
            Size = new Size(1180, 690); MinimumSize = new Size(900, 540);
            StartPosition = FormStartPosition.CenterParent; Tema.Janela(this);
            _itens = new BindingList<DefinicaoComponente>(CatalogoMaquinas.Dados.Itens.Select(Clonar).ToList());
            var cab = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Tema.Fundo };
            var titulo = Tema.Titulo("Padrões de máquinas", 15f); titulo.Location = new Point(18, 10);
            var subtitulo = Tema.Subtitulo("Edite códigos e descrições ou cadastre novos componentes e perfis de lógica."); subtitulo.Location = new Point(21, 45);
            cab.Controls.Add(titulo); cab.Controls.Add(subtitulo);
            _grade = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, DataSource = _itens, AllowUserToAddRows = false, AllowUserToDeleteRows = false, BackgroundColor = Tema.Fundo, BorderStyle = BorderStyle.None, RowHeadersVisible = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells };
            _grade.EnableHeadersVisualStyles = false; _grade.ColumnHeadersDefaultCellStyle.BackColor = Tema.FundoEscuro; _grade.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grade.DefaultCellStyle.BackColor = Tema.Cartao; _grade.DefaultCellStyle.ForeColor = Color.White; _grade.DefaultCellStyle.SelectionBackColor = Tema.Azul; _grade.DefaultCellStyle.SelectionForeColor = Color.White;
            Coluna("Categoria", "Categoria", 100); Coluna("Código", "Codigo", 75); Coluna("Nome", "Nome", 155); Coluna("Tipo padrão", "TipoPadrao", 190); Coluna("Descrição padrão", "DescricaoPadrao", 230); Coluna("Perfil de geração", "PerfilGeracao", 145);
            _grade.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Incluir por padrão", DataPropertyName = "IncluirPorPadrao", Width = 105 });
            _grade.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Agitador", DataPropertyName = "AgitadorPorPadrao", Width = 70 });
            _grade.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Pode PID", DataPropertyName = "PodePid", Width = 70 });
            Coluna("Modulação padrão", "ModulacaoPadrao", 140); Coluna("Nível padrão", "NivelPadrao", 145);
            var ativo = new DataGridViewCheckBoxColumn { HeaderText = "Ativo", DataPropertyName = "Ativo", Width = 55 }; _grade.Columns.Add(ativo);
            var rodape = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = Tema.FundoEscuro };
            rodape.Controls.Add(Tema.Botao("Salvar padrões", true, delegate { Salvar(); }));
            rodape.Controls.Add(Tema.Botao("Remover selecionado", false, delegate { Remover(); }, "lixeira"));
            rodape.Controls.Add(Tema.Botao("Novo equipamento", false, delegate { Novo("Equipamento"); }, "mais"));
            rodape.Controls.Add(Tema.Botao("Novo químico", false, delegate { Novo("Químico"); }, "mais"));
            rodape.Controls.Add(Tema.Botao("Novo componente", false, delegate { Novo("Componente"); }, "mais"));
            var aviso = Tema.Ajuda("Perfil Existente usa uma lógica já implementada no Genesis. Personalizada registra a definição para avaliação; não cria automaticamente blocos, intertravamentos ou XML. Isso exige implementar e validar um gerador específico.", 1050);
            aviso.Dock = DockStyle.Bottom;
            Controls.Add(_grade); Controls.Add(aviso); Controls.Add(rodape); Controls.Add(cab);
        }
        private void Coluna(string titulo, string prop, int largura) { _grade.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = titulo, DataPropertyName = prop, Width = largura }); }
        private static DefinicaoComponente Clonar(DefinicaoComponente x) { return new DefinicaoComponente { Categoria=x.Categoria, Codigo=x.Codigo, Nome=x.Nome, TipoPadrao=x.TipoPadrao, DescricaoPadrao=x.DescricaoPadrao, PerfilGeracao=x.PerfilGeracao, Ativo=x.Ativo, IncluirPorPadrao=x.IncluirPorPadrao, AgitadorPorPadrao=x.AgitadorPorPadrao, PodePid=x.PodePid, ModulacaoPadrao=x.ModulacaoPadrao, NivelPadrao=x.NivelPadrao }; }
        private void Novo(string categoria)
        {
            _itens.Add(new DefinicaoComponente { Categoria = categoria, Codigo = "NOV", Nome = "Novo " + categoria.ToLowerInvariant(), TipoPadrao = "Partida direta", DescricaoPadrao = "", PerfilGeracao = "Personalizada", Ativo = true });
            _grade.CurrentCell = _grade.Rows[_grade.Rows.Count - 1].Cells[1]; _grade.BeginEdit(true);
        }
        private void Remover()
        {
            if (_grade.CurrentRow == null || _grade.CurrentRow.DataBoundItem == null) return;
            var item = (DefinicaoComponente)_grade.CurrentRow.DataBoundItem;
            if (MessageBox.Show("Remover '" + item.Codigo + " — " + item.Nome + "' do catálogo? Projetos existentes não serão alterados.", "Genesis", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) _itens.Remove(item);
        }
        private void Salvar()
        {
            _grade.EndEdit();
            var duplicados = _itens.GroupBy(x => (x.Categoria + "|" + (x.Codigo ?? "").Trim()).ToUpperInvariant()).Where(g => g.Count() > 1).ToList();
            if (duplicados.Count > 0) { MessageBox.Show("Há códigos duplicados na mesma categoria: " + string.Join(", ", duplicados.Select(x => x.Key.Split('|').Last()).ToArray()), "Genesis", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            foreach (var x in _itens)
            {
                x.Codigo = (x.Codigo ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(x.Categoria) || string.IsNullOrWhiteSpace(x.Codigo) || string.IsNullOrWhiteSpace(x.Nome)) { MessageBox.Show("Categoria, código e nome são obrigatórios.", "Genesis", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (x.Codigo.Length > 8 || x.Codigo.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')) || char.IsDigit(x.Codigo[0])) { MessageBox.Show("Código inválido: " + x.Codigo + ". Use até 8 caracteres, começando por letra e sem espaços.", "Genesis", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            }
            CatalogoMaquinas.Dados.Itens = _itens.Select(Clonar).ToList();
            try { CatalogoMaquinas.Salvar(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { MessageBox.Show("Não foi possível salvar o catálogo: " + ex.Message, "Genesis", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
