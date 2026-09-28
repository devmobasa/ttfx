using System.Text;

namespace Ttfx.Cli;

// Keep completion output compatible with clap_complete while deriving every command from the registry.
internal static class CompletionGenerator
{
    private sealed record Node( string Name, string Description, string Id, string Context, int Depth, OptionDescriptor[] Options, Node[] Children );

    internal static string Generate( string shell )
    {
        Node[] effects = CommandRegistry.Effects.Select( e => new Node( e.Name, e.Description, $"ttfx__subcmd__{e.Name}", $"ttfx {e.Name}", 2, e.Options, [] ) ).ToArray( );
        const string helpDescription = "Print this message or the help of the given subcommand(s)";
        Node[] helpChildren = effects.Select( e => e with { Id = $"ttfx__subcmd__help__subcmd__{e.Name}", Context = $"ttfx help {e.Name}", Depth = 3, Options = [] } ).Append( new( "help", helpDescription, "ttfx__subcmd__help__subcmd__help", "ttfx help help", 3, [], [] ) ).ToArray( );
        var help = new Node( "help", helpDescription, "ttfx__subcmd__help", "ttfx help", 2, [], helpChildren );
        string[] hiddenOrder = ["--m0-dump", "--parity-dump", "--max-frames", "--virtual-clock"];
        OptionDescriptor[] rootOptions = CommandRegistry.RootOptions.Where( o => !o.Hidden && o.Name != "--help" ).Concat( hiddenOrder.Select( name => CommandRegistry.RootOptions.Single( o => o.Name == name ) ) ).Append( CommandRegistry.RootOptions.Single( o => o.Name == "--help" ) ).ToArray( );
        var root = new Node( "ttfx", "", "ttfx", "ttfx", 1, rootOptions, [.. effects, help] );
        var output = new StringBuilder( );

        if ( shell == "bash" )
        {
            Bash( output, root );
        }
        else
        {
            Zsh( output, root );
        }

        return output.ToString( );
    }

    private static IEnumerable<Node> Ordered( Node node )
    {
        yield return node;

        foreach ( var child in node.Children.OrderBy( c => c.Name, StringComparer.Ordinal ) )
        {
            foreach ( var descendant in Ordered( child ) )
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<string> Names( OptionDescriptor option ) => option.Alias is null ? [option.Name] : [option.Name, option.Alias];

    private static void Bash( StringBuilder output, Node root )
    {
        output.Append( "_ttfx() {\n    local i cur prev opts cmd\n    COMPREPLY=()\n    if [[ \"${BASH_VERSINFO[0]}\" -ge 4 ]]; then\n        cur=\"$2\"\n    else\n        cur=\"${COMP_WORDS[COMP_CWORD]}\"\n    fi\n    prev=\"$3\"\n    cmd=\"\"\n    opts=\"\"\n\n    for i in \"${COMP_WORDS[@]:0:COMP_CWORD}\"\n    do\n        case \"${cmd},${i}\" in\n            \",$1\")\n                cmd=\"ttfx\"\n                ;;\n" );
        BashTransitions( output, root );
        output.Append( "            *)\n                ;;\n        esac\n    done\n\n    case \"${cmd}\" in\n" );

        foreach ( var node in Ordered( root ) )
        {
            var words = node.Options.Where( o => o.Alias is not null ).Select( o => o.Alias! ).Concat( node.Options.Select( o => o.Name ) ).Concat( node.Children.Select( c => c.Name ) );
            output.Append( "        " ).Append( node.Id ).Append( ")\n            opts=\"" ).AppendJoin( ' ', words ).Append( "\"\n            if [[ ${cur} == -* || ${COMP_CWORD} -eq " ).Append( node.Depth ).Append( " ]] ; then\n                COMPREPLY=( $(compgen -W \"${opts}\" -- \"${cur}\") )\n                return 0\n            fi\n            case \"${prev}\" in\n" );

            foreach ( var option in node.Options.Where( o => o.Metavariable is not null ) )
            {
                foreach ( string name in Names( option ) )
                {
                    output.Append( "                " ).Append( name ).Append( ")\n                    COMPREPLY=(" ).Append( option.Name == "--print-completion" ? "$(compgen -W \"bash zsh\" -- \"${cur}\")" : option.Name == "--slice-direction" ? "$(compgen -W \"vertical horizontal diagonal\" -- \"${cur}\")" : "$(compgen -f \"${cur}\")" ).Append( ")\n                    return 0\n                    ;;\n" );
                }
            }

            output.Append( "                *)\n                    COMPREPLY=()\n                    ;;\n            esac\n            COMPREPLY=( $(compgen -W \"${opts}\" -- \"${cur}\") )\n            return 0\n            ;;\n" );
        }

        output.Append( "    esac\n}\n\nif [[ \"${BASH_VERSINFO[0]}\" -eq 4 && \"${BASH_VERSINFO[1]}\" -ge 4 || \"${BASH_VERSINFO[0]}\" -gt 4 ]]; then\n    complete -F _ttfx -o nosort -o bashdefault -o default ttfx\nelse\n    complete -F _ttfx -o bashdefault -o default ttfx\nfi\n" );
    }

    private static void BashTransitions( StringBuilder output, Node parent )
    {
        foreach ( var child in parent.Children.OrderBy( c => c.Name, StringComparer.Ordinal ) )
        {
            output.Append( "            " ).Append( parent.Id ).Append( ',' ).Append( child.Name ).Append( ")\n                cmd=\"" ).Append( child.Id ).Append( "\"\n                ;;\n" );

        }

        foreach ( var child in parent.Children.OrderBy( c => c.Name, StringComparer.Ordinal ) )
        {
            BashTransitions( output, child );
        }
    }

    private static string Escape( string value ) => value.Replace( "'", "'\\''", StringComparison.Ordinal ).Replace( ":", "\\:", StringComparison.Ordinal ).Replace( "[", "\\[", StringComparison.Ordinal ).Replace( "]", "\\]", StringComparison.Ordinal );

    private static string Description( OptionDescriptor option )
    {
        int suffix = option.Description.IndexOf( " [default:", StringComparison.Ordinal );
        string value = suffix < 0 ? option.Description : option.Description[..suffix];
        suffix = value.IndexOf( " [possible values:", StringComparison.Ordinal );
        value = suffix < 0 ? value : value[..suffix];
        return value.StartsWith( "[default:", StringComparison.Ordinal ) || value.StartsWith( "[possible values:", StringComparison.Ordinal ) ? "" : Escape( value );
    }

    private static void ZshArguments( StringBuilder output, Node node, bool root = false )
    {
        output.Append( root ? "    " : "" ).Append( "_arguments \"${_arguments_options[@]}\" : \\\n" );

        foreach ( var option in node.Options.OrderBy( o => o.Metavariable is null ? 1 : 0 ) )
        {
            foreach ( string name in Names( option ).Reverse( ) )
            {
                output.Append( '\'' );

                if ( option.Name == "--include-effects" )
                {
                    output.Append( "(--exclude-effects)" );
                }

                output.Append( option.Multiple ? "*" : "" ).Append( name );

                if ( option.Metavariable is not null )
                {
                    output.Append( name.StartsWith( "--", StringComparison.Ordinal ) ? '=' : '+' );
                }

                output.Append( '[' ).Append( Description( option ) ).Append( ']' );

                if ( option.Metavariable is not null )
                {
                    output.Append( ':' ).Append( option.Metavariable ).Append( ':' ).Append( option.Name switch { "--input-file" => "_files", "--print-completion" => "(bash zsh)", "--slice-direction" => "(vertical horizontal diagonal)", _ => "_default" } );
                }

                output.Append( "' \\\n" );
            }
        }

        if ( node.Children.Length != 0 )
        {
            output.Append( "\":: :_" ).Append( node.Id ).Append( "_commands\" \\\n\"*::: :->" ).Append( node.Name ).Append( "\" \\\n" );
        }

        output.Append( "&& ret=0\n" );

        if ( node.Children.Length == 0 )
        {
            return;
        }

        if ( !root )
        {
            output.Append( '\n' );
        }

        output.Append( "    case $state in\n    (" ).Append( node.Name ).Append( ")\n        words=($line[1] \"${words[@]}\")\n        (( CURRENT += 1 ))\n        curcontext=\"${curcontext%:*:*}:" ).Append( node.Context.Replace( ' ', '-' ) ).Append( "-command-$line[1]:\"\n        case $line[1] in\n            " );

        foreach ( var child in node.Children )
        {
            output.Append( '(' ).Append( child.Name ).Append( ")\n" );
            ZshArguments( output, child );
            output.Append( ";;\n" );
        }

        output.Append( "        esac\n    ;;\nesac\n" );
    }

    private static void Zsh( StringBuilder output, Node root )
    {
        output.Append( "#compdef ttfx\n\nautoload -U is-at-least\n\n_ttfx() {\n    typeset -A opt_args\n    typeset -a _arguments_options\n    local ret=1\n\n    if is-at-least 5.2; then\n        _arguments_options=(-s -S -C)\n    else\n        _arguments_options=(-s -C)\n    fi\n\n    local context curcontext=\"$curcontext\" state line\n" );
        ZshArguments( output, root, true );
        output.Append( "}\n\n" );

        foreach ( var node in Ordered( root ) )
        {
            output.Append( "(( $+functions[_" ).Append( node.Id ).Append( "_commands] )) ||\n_" ).Append( node.Id ).Append( "_commands() {\n    local commands; commands=(" );

            if ( node.Children.Length != 0 )
            {
                output.Append( '\n' );

                foreach ( var child in node.Children )
                {
                    output.Append( '\'' ).Append( child.Name ).Append( ':' ).Append( Escape( child.Description ) ).Append( "' \\\n" );
                }

                output.Append( "    " );
            }

            output.Append( ")\n    _describe -t commands '" ).Append( node.Context ).Append( " commands' commands \"$@\"\n}\n" );
        }

        output.Append( "\nif [ \"$funcstack[1]\" = \"_ttfx\" ]; then\n    _ttfx \"$@\"\nelse\n    compdef _ttfx ttfx\nfi\n" );
    }
}
